using System.Runtime.InteropServices;
using SaturnEngine.SEMath;
using Silk.NET.OpenAL;

namespace SaturnEngine.SEAudio
{
    /// <summary>
    /// 基于 OpenAL Soft 的音频后端，提供完整的 3D 空间音频（距离衰减、锥形指向、多普勒）。
    /// </summary>
    public sealed unsafe class OpenALAudioBackend : IAudioBackend
    {
        private AL? _al;
        private ALContext? _alc;
        private Device* _device;
        private Context* _context;

        private readonly HashSet<uint> _buffers = new();
        private readonly HashSet<uint> _sources = new();

        public string BackendName => "OpenAL";
        public bool IsInitialized { get; private set; }
        public bool SupportsSpatialAudio => true;

        public string[] GetDeviceNames()
        {
            try
            {
                var alc = _alc ?? ALContext.GetApi();
                string spec = alc.GetContextProperty(null, GetContextString.DeviceSpecifier);
                return string.IsNullOrEmpty(spec) ? Array.Empty<string>() : new[] { spec };
            }
            catch
            {
                // 设备查询失败时返回空列表，调用方应使用默认设备。
                return Array.Empty<string>();
            }
        }

        public void Initialize(string? deviceName = null)
        {
            if (IsInitialized)
                return;

            _al = AL.GetApi();
            _alc = ALContext.GetApi();

            _device = _alc.OpenDevice(deviceName ?? string.Empty);
            if (_device == null)
                throw new InvalidOperationException($"OpenAL 无法打开音频设备: {deviceName ?? "<default>"}");

            _context = _alc.CreateContext(_device, null);
            if (_context == null)
            {
                _alc.CloseDevice(_device);
                _device = null;
                throw new InvalidOperationException("OpenAL 无法创建音频上下文。");
            }

            if (!_alc.MakeContextCurrent(_context))
            {
                _alc.DestroyContext(_context);
                _alc.CloseDevice(_device);
                _context = null;
                _device = null;
                throw new InvalidOperationException("OpenAL 无法激活音频上下文。");
            }

            IsInitialized = true;

            SetDistanceModel(SEDistanceModel.InverseDistanceClamped);
            SetListener(SEListenerState.Default);
        }

        public void Shutdown()
        {
            if (!IsInitialized)
                return;

            var al = _al!;
            foreach (var s in _sources)
            {
                al.SourceStop(s);
                al.DeleteSource(s);
            }
            _sources.Clear();

            foreach (var b in _buffers)
                al.DeleteBuffer(b);
            _buffers.Clear();

            var alc = _alc!;
            alc.MakeContextCurrent(null);
            if (_context != null)
            {
                alc.DestroyContext(_context);
                _context = null;
            }
            if (_device != null)
            {
                alc.CloseDevice(_device);
                _device = null;
            }

            IsInitialized = false;
        }

        #region 缓冲区

        public SEAudioBuffer CreateBuffer(ReadOnlySpan<byte> data, SEAudioFormat format, int sampleRate)
        {
            EnsureInitialized();
            var al = _al!;

            uint buffer = al.GenBuffer();
            switch (format)
            {
                case SEAudioFormat.Mono8:
                    al.BufferData<byte>(buffer, BufferFormat.Mono8, data.ToArray(), sampleRate);
                    break;
                case SEAudioFormat.Mono16:
                    al.BufferData<byte>(buffer, BufferFormat.Mono16, data.ToArray(), sampleRate);
                    break;
                case SEAudioFormat.Stereo8:
                    al.BufferData<byte>(buffer, BufferFormat.Stereo8, data.ToArray(), sampleRate);
                    break;
                case SEAudioFormat.Stereo16:
                    al.BufferData<byte>(buffer, BufferFormat.Stereo16, data.ToArray(), sampleRate);
                    break;
                case SEAudioFormat.MonoFloat32:
                case SEAudioFormat.StereoFloat32:
                {
                    // 核心 OpenAL 只保证支持 8/16 位整数，这里统一转换为 16 位以保证各平台一致。
                    var pcm16 = ConvertFloat32ToPcm16(data);
                    var target = format == SEAudioFormat.MonoFloat32 ? BufferFormat.Mono16 : BufferFormat.Stereo16;
                    al.BufferData<byte>(buffer, target, pcm16, sampleRate);
                    break;
                }
                default:
                    al.DeleteBuffer(buffer);
                    throw new ArgumentOutOfRangeException(nameof(format));
            }

            _buffers.Add(buffer);
            return new SEAudioBuffer(buffer);
        }

        private static byte[] ConvertFloat32ToPcm16(ReadOnlySpan<byte> data)
        {
            var samples = MemoryMarshal.Cast<byte, float>(data);
            var result = new byte[samples.Length * sizeof(short)];
            Span<short> dst = MemoryMarshal.Cast<byte, short>(result.AsSpan());
            for (int i = 0; i < samples.Length; i++)
            {
                float v = Math.Clamp(samples[i], -1.0f, 1.0f);
                dst[i] = (short)(v * short.MaxValue);
            }
            return result;
        }

        public void DestroyBuffer(SEAudioBuffer buffer)
        {
            if (!IsInitialized || !buffer.IsValid)
                return;
            if (_buffers.Remove(buffer.Id))
                _al!.DeleteBuffer(buffer.Id);
        }

        #endregion

        #region 音源

        public SEAudioSource CreateSource()
        {
            EnsureInitialized();
            uint source = _al!.GenSource();
            _sources.Add(source);
            return new SEAudioSource(source);
        }

        public void DestroySource(SEAudioSource source)
        {
            if (!IsInitialized || !source.IsValid)
                return;
            if (_sources.Remove(source.Id))
            {
                _al!.SourceStop(source.Id);
                _al.DeleteSource(source.Id);
            }
        }

        public void AttachBuffer(SEAudioSource source, SEAudioBuffer buffer)
        {
            EnsureInitialized();
            _al!.SetSourceProperty(source.Id, SourceInteger.Buffer, (int)buffer.Id);
        }

        public void Play(SEAudioSource source)
        {
            EnsureInitialized();
            _al!.SourcePlay(source.Id);
        }

        public void Pause(SEAudioSource source)
        {
            EnsureInitialized();
            _al!.SourcePause(source.Id);
        }

        public void Stop(SEAudioSource source)
        {
            EnsureInitialized();
            _al!.SourceStop(source.Id);
        }

        public SEAudioState GetState(SEAudioSource source)
        {
            EnsureInitialized();
            _al!.GetSourceProperty(source.Id, GetSourceInteger.SourceState, out int state);
            return (SourceState)state switch
            {
                SourceState.Playing => SEAudioState.Playing,
                SourceState.Paused => SEAudioState.Paused,
                SourceState.Stopped => SEAudioState.Stopped,
                _ => SEAudioState.Initial,
            };
        }

        public void SetSourceParams(SEAudioSource source, in SESourceParams parameters)
        {
            EnsureInitialized();
            var al = _al!;
            uint id = source.Id;

            al.SetSourceProperty(id, SourceVector3.Position, (float)parameters.Position.X, (float)parameters.Position.Y, (float)parameters.Position.Z);
            al.SetSourceProperty(id, SourceVector3.Velocity, (float)parameters.Velocity.X, (float)parameters.Velocity.Y, (float)parameters.Velocity.Z);
            al.SetSourceProperty(id, SourceVector3.Direction, (float)parameters.Direction.X, (float)parameters.Direction.Y, (float)parameters.Direction.Z);

            al.SetSourceProperty(id, SourceFloat.Gain, parameters.Gain);
            al.SetSourceProperty(id, SourceFloat.Pitch, parameters.Pitch);
            al.SetSourceProperty(id, SourceFloat.ReferenceDistance, parameters.ReferenceDistance);
            al.SetSourceProperty(id, SourceFloat.MaxDistance, parameters.MaxDistance);
            al.SetSourceProperty(id, SourceFloat.RolloffFactor, parameters.RolloffFactor);
            al.SetSourceProperty(id, SourceFloat.ConeInnerAngle, parameters.ConeInnerAngle);
            al.SetSourceProperty(id, SourceFloat.ConeOuterAngle, parameters.ConeOuterAngle);
            al.SetSourceProperty(id, SourceFloat.ConeOuterGain, parameters.ConeOuterGain);

            al.SetSourceProperty(id, SourceBoolean.Looping, parameters.Looping);
            al.SetSourceProperty(id, SourceBoolean.SourceRelative, parameters.RelativeToListener);
        }

        public void SetSourceTransform(SEAudioSource source, in Vector3D position, in Vector3D velocity)
        {
            EnsureInitialized();
            var al = _al!;
            al.SetSourceProperty(source.Id, SourceVector3.Position, (float)position.X, (float)position.Y, (float)position.Z);
            al.SetSourceProperty(source.Id, SourceVector3.Velocity, (float)velocity.X, (float)velocity.Y, (float)velocity.Z);
        }

        #endregion

        #region 监听器与全局设置

        public void SetListener(in SEListenerState listener)
        {
            EnsureInitialized();
            var al = _al!;

            al.SetListenerProperty(ListenerVector3.Position, (float)listener.Position.X, (float)listener.Position.Y, (float)listener.Position.Z);
            al.SetListenerProperty(ListenerVector3.Velocity, (float)listener.Velocity.X, (float)listener.Velocity.Y, (float)listener.Velocity.Z);
            al.SetListenerProperty(ListenerFloat.Gain, listener.Gain);

            // OpenAL 的朝向由「前向量 + 上向量」共 6 个分量组成。
            Span<float> orientation = stackalloc float[6]
            {
                (float)listener.Forward.X, (float)listener.Forward.Y, (float)listener.Forward.Z,
                (float)listener.Up.X, (float)listener.Up.Y, (float)listener.Up.Z,
            };
            fixed (float* p = orientation)
            {
                al.SetListenerProperty(ListenerFloatArray.Orientation, p);
            }
        }

        public void SetDistanceModel(SEDistanceModel model)
        {
            EnsureInitialized();
            _al!.DistanceModel(model switch
            {
                SEDistanceModel.None => DistanceModel.None,
                SEDistanceModel.InverseDistance => DistanceModel.InverseDistance,
                SEDistanceModel.InverseDistanceClamped => DistanceModel.InverseDistanceClamped,
                SEDistanceModel.LinearDistance => DistanceModel.LinearDistance,
                SEDistanceModel.LinearDistanceClamped => DistanceModel.LinearDistanceClamped,
                SEDistanceModel.ExponentDistance => DistanceModel.ExponentDistance,
                SEDistanceModel.ExponentDistanceClamped => DistanceModel.ExponentDistanceClamped,
                _ => DistanceModel.InverseDistanceClamped,
            });
        }

        public void SetDopplerFactor(float factor)
        {
            EnsureInitialized();
            _al!.DopplerFactor(Math.Max(0.0f, factor));
        }

        public void SetSpeedOfSound(float speed)
        {
            EnsureInitialized();
            _al!.SpeedOfSound(speed);
        }

        public void SetMasterGain(float gain)
        {
            EnsureInitialized();
            _al!.SetListenerProperty(ListenerFloat.Gain, Math.Max(0.0f, gain));
        }

        #endregion

        public void Update(double deltaTime)
        {
            // OpenAL 在驱动内部完成混音，静态缓冲播放无需逐帧处理。
            // 流式播放接入后，这里将负责回收已播放完的队列缓冲。
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("OpenAL 音频后端尚未初始化。");
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}
