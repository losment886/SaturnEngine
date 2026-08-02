
using SaturnEngine.Base;
using SaturnEngine.Management;
using SaturnEngine.SEMath;

namespace SaturnEngine.SEAudio
{
    /// <summary>后端选择策略。</summary>
    public enum SEAudioBackendType
    {
        /// <summary>优先 OpenAL，失败时回退 SDL。</summary>
        Auto,
        OpenAL,
        SDL,
    }

    /// <summary>
    /// 引擎音频管理器：持有具体 <see cref="IAudioBackend"/>，统一管理音源、监听器与逐帧更新。
    /// </summary>
    public class SEAudioManager : SEBase, IDisposable
    {
        /// <summary>一个可播放的音频通道，封装后端音源与其空间参数。</summary>
        public class SEChannel
        {
            public SEAudioSource Source { get; internal set; }
            public SEAudioBuffer Buffer { get; internal set; }
            public SESourceParams Params = SESourceParams.Default;
            internal SEAudioManager? Manager;

            public bool IsValid => Source.IsValid && Manager is not null;

            public void Play() => Manager?.Backend?.Play(Source);
            public void Pause() => Manager?.Backend?.Pause(Source);
            public void Stop() => Manager?.Backend?.Stop(Source);

            public SEAudioState State
                => Manager?.Backend is { } b && Source.IsValid ? b.GetState(Source) : SEAudioState.Initial;

            /// <summary>把本地 <see cref="Params"/> 的修改提交到后端。</summary>
            public void Apply() => Manager?.Backend?.SetSourceParams(Source, in Params);

            /// <summary>仅同步位置与速度，适合逐帧调用。</summary>
            public void SetTransform(Vector3D position, Vector3D velocity)
            {
                Params.Position = position;
                Params.Velocity = velocity;
                Manager?.Backend?.SetSourceTransform(Source, in position, in velocity);
            }
        }

        public IAudioBackend? Backend { get; private set; }
        public bool IsEnable { get; private set; } = false;
        public SEAudioBackendType BackendType { get; private set; } = SEAudioBackendType.Auto;
        public bool SupportsSpatialAudio => Backend?.SupportsSpatialAudio ?? false;

        public SEListenerState Listener { get; private set; } = SEListenerState.Default;
        public IReadOnlyList<SEChannel> Channels => _channels;

        private readonly List<SEChannel> _channels = new();

        public SEAudioManager() : base("SEAudioManager", "引擎音频管理器")
        {
        }

        /// <summary>初始化音频后端。<paramref name="deviceName"/> 为空时使用系统默认设备。</summary>
        public void Initialize(SEAudioBackendType type = SEAudioBackendType.Auto, string? deviceName = null)
        {
            if (IsEnable)
                return;

            BackendType = type;

            if ((type is SEAudioBackendType.Auto or SEAudioBackendType.OpenAL)
                && TryStart(new OpenALAudioBackend(), deviceName))
            {
                BackendType = SEAudioBackendType.OpenAL;
            }
            else if ((type is SEAudioBackendType.Auto or SEAudioBackendType.SDL)
                && TryStart(new SDLAudioBackend(), deviceName))
            {
                BackendType = SEAudioBackendType.SDL;
            }
            else
            {
                SELogger.Error("所有音频后端均初始化失败，音频功能已禁用。", "SEAudioManager");
                return;
            }

            IsEnable = true;
            Backend!.SetDistanceModel(SEDistanceModel.InverseDistanceClamped);
            SetListener(Listener);
            SELogger.Log($"音频后端已启用: {Backend.BackendName} (空间音频: {Backend.SupportsSpatialAudio})", "SEAudioManager");
        }

        private bool TryStart(IAudioBackend backend, string? deviceName)
        {
            try
            {
                backend.Initialize(deviceName);
                Backend = backend;
                return true;
            }
            catch (Exception ex)
            {
                SELogger.Warn($"音频后端 {backend.BackendName} 初始化失败: {ex.Message}", "SEAudioManager");
                backend.Dispose();
                return false;
            }
        }

        /// <summary>创建一个通道并绑定已解码的 PCM 数据。</summary>
        public SEChannel? CreateChannel(ReadOnlySpan<byte> pcm, SEAudioFormat format, int sampleRate)
        {
            if (!IsEnable || Backend is null)
                return null;

            var channel = new SEChannel
            {
                Manager = this,
                Buffer = Backend.CreateBuffer(pcm, format, sampleRate),
            };
            channel.Source = Backend.CreateSource();
            Backend.AttachBuffer(channel.Source, channel.Buffer);
            Backend.SetSourceParams(channel.Source, in channel.Params);

            _channels.Add(channel);
            return channel;
        }

        public void RemoveChannel(SEChannel channel)
        {
            if (!_channels.Remove(channel) || Backend is null)
                return;

            Backend.DestroySource(channel.Source);
            Backend.DestroyBuffer(channel.Buffer);
            channel.Manager = null;
        }

        /// <summary>更新监听器（通常每帧从摄像机同步）。</summary>
        public void SetListener(in SEListenerState listener)
        {
            Listener = listener;
            Backend?.SetListener(in listener);
        }

        public void SetMasterGain(float gain) => Backend?.SetMasterGain(gain);
        public void SetDistanceModel(SEDistanceModel model) => Backend?.SetDistanceModel(model);
        public void SetDopplerFactor(float factor) => Backend?.SetDopplerFactor(factor);
        public void SetSpeedOfSound(float speed) => Backend?.SetSpeedOfSound(speed);

        /// <summary>驱动后端的逐帧处理，应在音频线程或 EXTUpdate 中调用。</summary>
        public void Update(double deltaTime) => Backend?.Update(deltaTime);

        public void Dispose()
        {
            if (Backend is not null)
            {
                foreach (var c in _channels)
                {
                    Backend.DestroySource(c.Source);
                    Backend.DestroyBuffer(c.Buffer);
                    c.Manager = null;
                }
                Backend.Dispose();
                Backend = null;
            }
            _channels.Clear();
            IsEnable = false;
        }
    }
}
