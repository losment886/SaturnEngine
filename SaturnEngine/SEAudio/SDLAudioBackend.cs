using System.Runtime.InteropServices;
using SaturnEngine.SEMath;
using Silk.NET.SDL;

namespace SaturnEngine.SEAudio
{
    /// <summary>
    /// 基于 SDL 的跨平台回退音频后端。
    /// <para>
    /// SDL 本身不提供 3D 音频，这里在软件层做混音：按距离模型计算增益，
    /// 再按音源相对监听器的左右方向做立体声平移。效果弱于 OpenAL 的 HRTF，
    /// 但可在任何 SDL 可用的平台（含 Android / iOS / HarmonyOS NEXT）上工作。
    /// </para>
    /// </summary>
    public sealed unsafe class SDLAudioBackend : IAudioBackend
    {
        private const ushort AudioF32Lsb = 0x8120;
        private const int OutputChannels = 2;

        private sealed class BufferData
        {
            public float[] Samples = Array.Empty<float>();
            public int Channels;
            public int SampleRate;
        }

        private sealed class SourceData
        {
            public BufferData? Buffer;
            public SESourceParams Params = SESourceParams.Default;
            public SEAudioState State = SEAudioState.Initial;
            /// <summary>以输出采样率为单位的播放位置（可为小数，用于变调与重采样）。</summary>
            public double Position;
        }

        private Sdl? _sdl;
        private uint _deviceId;
        private int _sampleRate = 48000;

        private readonly Dictionary<uint, BufferData> _buffers = new();
        private readonly Dictionary<uint, SourceData> _sources = new();
        private uint _nextBufferId = 1;
        private uint _nextSourceId = 1;

        private SEListenerState _listener = SEListenerState.Default;
        private SEDistanceModel _distanceModel = SEDistanceModel.InverseDistanceClamped;
        private float _masterGain = 1.0f;
        private float _dopplerFactor = 1.0f;
        private float _speedOfSound = 343.3f;

        private float[] _mixBuffer = Array.Empty<float>();

        public string BackendName => "SDL";
        public bool IsInitialized { get; private set; }
        /// <summary>软件平移不是真正的 HRTF 空间化。</summary>
        public bool SupportsSpatialAudio => false;

        public string[] GetDeviceNames()
        {
            var sdl = _sdl ?? Sdl.GetApi();
            int count = sdl.GetNumAudioDevices(0);
            if (count <= 0)
                return Array.Empty<string>();

            var names = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                byte* p = sdl.GetAudioDeviceName(i, 0);
                if (p != null)
                {
                    string? n = Marshal.PtrToStringUTF8((nint)p);
                    if (!string.IsNullOrEmpty(n))
                        names.Add(n);
                }
            }
            return names.ToArray();
        }

        public void Initialize(string? deviceName = null)
        {
            if (IsInitialized)
                return;

            _sdl = Sdl.GetApi();
            if (_sdl.WasInit(Sdl.InitAudio) == 0 && _sdl.InitSubSystem(Sdl.InitAudio) != 0)
                throw new InvalidOperationException($"SDL 音频子系统初始化失败: {GetSdlError()}");

            var desired = new AudioSpec
            {
                Freq = _sampleRate,
                Format = AudioF32Lsb,
                Channels = (byte)OutputChannels,
                Samples = 1024,
            };
            AudioSpec obtained;

            uint id;
            if (string.IsNullOrEmpty(deviceName))
            {
                id = _sdl.OpenAudioDevice((byte*)null, 0, &desired, &obtained, 0);
            }
            else
            {
                var utf8 = System.Text.Encoding.UTF8.GetBytes(deviceName + "\0");
                fixed (byte* p = utf8)
                {
                    id = _sdl.OpenAudioDevice(p, 0, &desired, &obtained, 0);
                }
            }

            if (id == 0)
                throw new InvalidOperationException($"SDL 无法打开音频设备: {GetSdlError()}");

            _deviceId = id;
            _sampleRate = obtained.Freq;
            _sdl.PauseAudioDevice(_deviceId, 0);
            IsInitialized = true;
        }

        private string GetSdlError()
        {
            byte* e = _sdl!.GetError();
            return e == null ? "unknown" : Marshal.PtrToStringUTF8((nint)e) ?? "unknown";
        }

        public void Shutdown()
        {
            if (!IsInitialized)
                return;

            _sdl!.CloseAudioDevice(_deviceId);
            _deviceId = 0;
            _sources.Clear();
            _buffers.Clear();
            IsInitialized = false;
        }

        #region 缓冲区

        public SEAudioBuffer CreateBuffer(ReadOnlySpan<byte> data, SEAudioFormat format, int sampleRate)
        {
            EnsureInitialized();

            var bd = new BufferData { SampleRate = sampleRate };
            switch (format)
            {
                case SEAudioFormat.Mono8:
                case SEAudioFormat.Stereo8:
                    bd.Channels = format == SEAudioFormat.Mono8 ? 1 : 2;
                    bd.Samples = new float[data.Length];
                    for (int i = 0; i < data.Length; i++)
                        bd.Samples[i] = (data[i] - 128) / 128.0f;
                    break;

                case SEAudioFormat.Mono16:
                case SEAudioFormat.Stereo16:
                {
                    bd.Channels = format == SEAudioFormat.Mono16 ? 1 : 2;
                    var src = MemoryMarshal.Cast<byte, short>(data);
                    bd.Samples = new float[src.Length];
                    for (int i = 0; i < src.Length; i++)
                        bd.Samples[i] = src[i] / (float)short.MaxValue;
                    break;
                }

                case SEAudioFormat.MonoFloat32:
                case SEAudioFormat.StereoFloat32:
                    bd.Channels = format == SEAudioFormat.MonoFloat32 ? 1 : 2;
                    bd.Samples = MemoryMarshal.Cast<byte, float>(data).ToArray();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }

            uint id = _nextBufferId++;
            _buffers[id] = bd;
            return new SEAudioBuffer(id);
        }

        public void DestroyBuffer(SEAudioBuffer buffer)
        {
            _buffers.Remove(buffer.Id);
        }

        #endregion

        #region 音源

        public SEAudioSource CreateSource()
        {
            EnsureInitialized();
            uint id = _nextSourceId++;
            _sources[id] = new SourceData();
            return new SEAudioSource(id);
        }

        public void DestroySource(SEAudioSource source) => _sources.Remove(source.Id);

        public void AttachBuffer(SEAudioSource source, SEAudioBuffer buffer)
        {
            if (_sources.TryGetValue(source.Id, out var s) && _buffers.TryGetValue(buffer.Id, out var b))
            {
                s.Buffer = b;
                s.Position = 0;
            }
        }

        public void Play(SEAudioSource source)
        {
            if (_sources.TryGetValue(source.Id, out var s))
            {
                if (s.State != SEAudioState.Paused)
                    s.Position = 0;
                s.State = SEAudioState.Playing;
            }
        }

        public void Pause(SEAudioSource source)
        {
            if (_sources.TryGetValue(source.Id, out var s) && s.State == SEAudioState.Playing)
                s.State = SEAudioState.Paused;
        }

        public void Stop(SEAudioSource source)
        {
            if (_sources.TryGetValue(source.Id, out var s))
            {
                s.State = SEAudioState.Stopped;
                s.Position = 0;
            }
        }

        public SEAudioState GetState(SEAudioSource source)
            => _sources.TryGetValue(source.Id, out var s) ? s.State : SEAudioState.Initial;

        public void SetSourceParams(SEAudioSource source, in SESourceParams parameters)
        {
            if (_sources.TryGetValue(source.Id, out var s))
                s.Params = parameters;
        }

        public void SetSourceTransform(SEAudioSource source, in Vector3D position, in Vector3D velocity)
        {
            if (_sources.TryGetValue(source.Id, out var s))
            {
                s.Params.Position = position;
                s.Params.Velocity = velocity;
            }
        }

        #endregion

        #region 监听器与全局设置

        public void SetListener(in SEListenerState listener) => _listener = listener;
        public void SetDistanceModel(SEDistanceModel model) => _distanceModel = model;
        public void SetDopplerFactor(float factor) => _dopplerFactor = Math.Max(0.0f, factor);
        public void SetSpeedOfSound(float speed) => _speedOfSound = speed;
        public void SetMasterGain(float gain) => _masterGain = Math.Max(0.0f, gain);

        #endregion

        public void Update(double deltaTime)
        {
            if (!IsInitialized || deltaTime <= 0)
                return;

            // 保持大约两帧的缓冲余量，避免欠载爆音同时不引入过高延迟。
            int frames = (int)Math.Ceiling(_sampleRate * deltaTime);
            if (frames <= 0)
                return;

            uint queuedBytes = _sdl!.GetQueuedAudioSize(_deviceId);
            int queuedFrames = (int)(queuedBytes / (sizeof(float) * OutputChannels));
            int target = frames * 2;
            if (queuedFrames >= target)
                return;

            int needed = target - queuedFrames;
            int required = needed * OutputChannels;
            if (_mixBuffer.Length < required)
                _mixBuffer = new float[required];

            Array.Clear(_mixBuffer, 0, required);
            foreach (var s in _sources.Values)
                MixSource(s, _mixBuffer.AsSpan(0, required), needed);

            for (int i = 0; i < required; i++)
                _mixBuffer[i] = Math.Clamp(_mixBuffer[i] * _masterGain, -1.0f, 1.0f);

            fixed (float* p = _mixBuffer)
            {
                _sdl.QueueAudio(_deviceId, p, (uint)(required * sizeof(float)));
            }
        }

        private void MixSource(SourceData s, Span<float> output, int frames)
        {
            if (s.State != SEAudioState.Playing || s.Buffer is null)
                return;

            var buf = s.Buffer;
            int srcFrames = buf.Samples.Length / buf.Channels;
            if (srcFrames <= 0)
                return;

            ComputeSpatialGains(s, out float leftGain, out float rightGain, out float pitchScale);

            // 同时承载重采样与音高：步进 = 源采样率/输出采样率 * pitch。
            double step = (double)buf.SampleRate / _sampleRate * pitchScale;
            if (step <= 0)
                step = 1.0;

            double pos = s.Position;
            for (int i = 0; i < frames; i++)
            {
                if (pos >= srcFrames)
                {
                    if (s.Params.Looping)
                    {
                        pos -= srcFrames;
                    }
                    else
                    {
                        s.State = SEAudioState.Stopped;
                        s.Position = 0;
                        return;
                    }
                }

                int idx = (int)pos;
                float l, r;
                if (buf.Channels == 1)
                {
                    l = r = buf.Samples[idx];
                }
                else
                {
                    l = buf.Samples[idx * 2];
                    r = buf.Samples[idx * 2 + 1];
                }

                output[i * 2] += l * leftGain;
                output[i * 2 + 1] += r * rightGain;
                pos += step;
            }
            s.Position = pos;
        }

        private void ComputeSpatialGains(SourceData s, out float leftGain, out float rightGain, out float pitchScale)
        {
            var p = s.Params;
            pitchScale = p.Pitch <= 0 ? 1.0f : p.Pitch;

            // 立体声源与相对监听器的源不做空间化，与 OpenAL 的行为保持一致。
            if (p.RelativeToListener || (s.Buffer is not null && s.Buffer.Channels > 1))
            {
                leftGain = rightGain = p.Gain;
                return;
            }

            var toSource = new Vector3D(
                p.Position.X - _listener.Position.X,
                p.Position.Y - _listener.Position.Y,
                p.Position.Z - _listener.Position.Z);

            double distance = Math.Sqrt(toSource.X * toSource.X + toSource.Y * toSource.Y + toSource.Z * toSource.Z);
            float gain = p.Gain * _listener.Gain * ComputeDistanceGain((float)distance, p);
            pitchScale *= ComputeDopplerPitch(p, toSource, distance);

            if (distance < 1e-6)
            {
                leftGain = rightGain = gain;
                return;
            }

            // 由前向量与上向量导出右向量，再用它做左右平移。
            var right = Cross(_listener.Forward, _listener.Up);
            double rl = Math.Sqrt(right.X * right.X + right.Y * right.Y + right.Z * right.Z);
            double pan = rl < 1e-6
                ? 0
                : (toSource.X * right.X + toSource.Y * right.Y + toSource.Z * right.Z) / (rl * distance);
            pan = Math.Clamp(pan, -1.0, 1.0);

            // 等功率平移，保证平移过程中总能量恒定。
            double angle = (pan + 1.0) * 0.25 * Math.PI;
            leftGain = (float)(gain * Math.Cos(angle));
            rightGain = (float)(gain * Math.Sin(angle));
        }

        private float ComputeDopplerPitch(in SESourceParams p, in Vector3D toSource, double distance)
        {
            if (_dopplerFactor <= 0 || distance < 1e-6)
                return 1.0f;

            double invLen = 1.0 / distance;
            double ux = toSource.X * invLen, uy = toSource.Y * invLen, uz = toSource.Z * invLen;

            double vls = _listener.Velocity.X * ux + _listener.Velocity.Y * uy + _listener.Velocity.Z * uz;
            double vss = p.Velocity.X * ux + p.Velocity.Y * uy + p.Velocity.Z * uz;

            double ss = _speedOfSound / _dopplerFactor;
            vls = Math.Min(vls, ss);
            vss = Math.Min(vss, ss);

            double denominator = ss - vss;
            if (Math.Abs(denominator) < 1e-6)
                return 1.0f;

            return (float)Math.Clamp((ss - vls) / denominator, 0.5, 2.0);
        }

        private float ComputeDistanceGain(float distance, in SESourceParams p)
        {
            float refDist = Math.Max(p.ReferenceDistance, 1e-6f);
            float rolloff = p.RolloffFactor;
            float maxDist = p.MaxDistance;

            switch (_distanceModel)
            {
                case SEDistanceModel.None:
                    return 1.0f;

                case SEDistanceModel.InverseDistanceClamped:
                    distance = Math.Clamp(distance, refDist, maxDist);
                    goto case SEDistanceModel.InverseDistance;
                case SEDistanceModel.InverseDistance:
                    return refDist / (refDist + rolloff * (Math.Max(distance, refDist) - refDist));

                case SEDistanceModel.LinearDistanceClamped:
                    distance = Math.Clamp(distance, refDist, maxDist);
                    goto case SEDistanceModel.LinearDistance;
                case SEDistanceModel.LinearDistance:
                {
                    float span = maxDist - refDist;
                    if (span <= 1e-6f)
                        return 1.0f;
                    return Math.Clamp(1.0f - rolloff * (distance - refDist) / span, 0.0f, 1.0f);
                }

                case SEDistanceModel.ExponentDistanceClamped:
                    distance = Math.Clamp(distance, refDist, maxDist);
                    goto case SEDistanceModel.ExponentDistance;
                case SEDistanceModel.ExponentDistance:
                    return (float)Math.Pow(Math.Max(distance, refDist) / refDist, -rolloff);

                default:
                    return 1.0f;
            }
        }

        private static Vector3D Cross(in Vector3D a, in Vector3D b)
            => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        private void EnsureInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("SDL 音频后端尚未初始化。");
        }

        public void Dispose() => Shutdown();
    }
}
