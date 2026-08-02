using SaturnEngine.SEMath;

namespace SaturnEngine.SEAudio
{
    /// <summary>音频源句柄。0 表示无效句柄。</summary>
    public readonly struct SEAudioSource : IEquatable<SEAudioSource>
    {
        public readonly uint Id;
        public SEAudioSource(uint id) => Id = id;
        public bool IsValid => Id != 0;

        public bool Equals(SEAudioSource other) => Id == other.Id;
        public override bool Equals(object? obj) => obj is SEAudioSource s && Equals(s);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => $"SEAudioSource({Id})";
    }

    /// <summary>音频缓冲区句柄，代表一段已解码并上传到后端的 PCM 数据。</summary>
    public readonly struct SEAudioBuffer : IEquatable<SEAudioBuffer>
    {
        public readonly uint Id;
        public SEAudioBuffer(uint id) => Id = id;
        public bool IsValid => Id != 0;

        public bool Equals(SEAudioBuffer other) => Id == other.Id;
        public override bool Equals(object? obj) => obj is SEAudioBuffer b && Equals(b);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => $"SEAudioBuffer({Id})";
    }

    /// <summary>PCM 采样格式。</summary>
    public enum SEAudioFormat
    {
        Mono8,
        Mono16,
        Stereo8,
        Stereo16,
        MonoFloat32,
        StereoFloat32,
    }

    /// <summary>
    /// 距离衰减模型。
    /// <para>
    /// 只有单声道音源能够被空间化，立体声数据在所有后端上都会被直接输出而不做 3D 处理，
    /// 这是 OpenAL 的既定行为，也是空间音频的通用约定。
    /// </para>
    /// </summary>
    public enum SEDistanceModel
    {
        None,
        InverseDistance,
        InverseDistanceClamped,
        LinearDistance,
        LinearDistanceClamped,
        ExponentDistance,
        ExponentDistanceClamped,
    }

    /// <summary>音源播放状态。</summary>
    public enum SEAudioState
    {
        Initial,
        Playing,
        Paused,
        Stopped,
    }

    /// <summary>
    /// 监听器（"耳朵"）的空间状态。通常每帧从摄像机同步一次。
    /// </summary>
    public struct SEListenerState
    {
        public Vector3D Position;
        /// <summary>用于多普勒计算的速度，单位为「单位/秒」。</summary>
        public Vector3D Velocity;
        /// <summary>朝向（前方向量）。</summary>
        public Vector3D Forward;
        /// <summary>上方向量，与 <see cref="Forward"/> 共同确定朝向。</summary>
        public Vector3D Up;
        public float Gain;

        public static SEListenerState Default => new()
        {
            Position = new Vector3D(0, 0, 0),
            Velocity = new Vector3D(0, 0, 0),
            Forward = new Vector3D(0, 0, -1),
            Up = new Vector3D(0, 1, 0),
            Gain = 1.0f,
        };
    }

    /// <summary>
    /// 音源的空间参数。
    /// </summary>
    public struct SESourceParams
    {
        public Vector3D Position;
        public Vector3D Velocity;
        /// <summary>锥形指向的朝向；为零向量时音源为全向。</summary>
        public Vector3D Direction;

        public float Gain;
        public float Pitch;

        /// <summary>参考距离：在此距离内音量不衰减。</summary>
        public float ReferenceDistance;
        /// <summary>最大距离：超过后不再继续衰减（Clamped 模型下生效）。</summary>
        public float MaxDistance;
        /// <summary>衰减系数，越大衰减越快。</summary>
        public float RolloffFactor;

        /// <summary>内锥角度（度）。锥内为全增益。</summary>
        public float ConeInnerAngle;
        /// <summary>外锥角度（度）。锥外应用 <see cref="ConeOuterGain"/>。</summary>
        public float ConeOuterAngle;
        public float ConeOuterGain;

        public bool Looping;
        /// <summary>为 true 时坐标相对于监听器，用于 UI 音效等不需要空间化的场合。</summary>
        public bool RelativeToListener;

        public static SESourceParams Default => new()
        {
            Position = new Vector3D(0, 0, 0),
            Velocity = new Vector3D(0, 0, 0),
            Direction = new Vector3D(0, 0, 0),
            Gain = 1.0f,
            Pitch = 1.0f,
            ReferenceDistance = 1.0f,
            MaxDistance = 1000.0f,
            RolloffFactor = 1.0f,
            ConeInnerAngle = 360.0f,
            ConeOuterAngle = 360.0f,
            ConeOuterGain = 0.0f,
            Looping = false,
            RelativeToListener = false,
        };
    }

    /// <summary>
    /// 音频后端抽象。
    /// <para>
    /// 引擎通过该接口驱动具体音频实现，目前提供 OpenAL（完整空间音频）
    /// 与 SDL（跨平台回退）两种实现。所有方法都应在音频线程上调用。
    /// </para>
    /// </summary>
    public interface IAudioBackend : IDisposable
    {
        /// <summary>后端名称，用于日志与诊断。</summary>
        string BackendName { get; }

        /// <summary>后端是否已成功初始化。</summary>
        bool IsInitialized { get; }

        /// <summary>该后端是否支持真正的 3D 空间化（HRTF 或等效算法）。</summary>
        bool SupportsSpatialAudio { get; }

        /// <summary>枚举可用的输出设备名称。</summary>
        string[] GetDeviceNames();

        /// <summary>初始化后端并打开设备。<paramref name="deviceName"/> 为 null 时使用系统默认设备。</summary>
        void Initialize(string? deviceName = null);

        /// <summary>关闭设备并释放全部后端资源。</summary>
        void Shutdown();

        #region 缓冲区

        /// <summary>上传一段 PCM 数据并返回缓冲区句柄。</summary>
        SEAudioBuffer CreateBuffer(ReadOnlySpan<byte> data, SEAudioFormat format, int sampleRate);

        /// <summary>释放缓冲区。调用方须保证没有音源仍在使用它。</summary>
        void DestroyBuffer(SEAudioBuffer buffer);

        #endregion

        #region 音源

        SEAudioSource CreateSource();
        void DestroySource(SEAudioSource source);

        /// <summary>把缓冲区绑定到音源。绑定前音源必须处于停止状态。</summary>
        void AttachBuffer(SEAudioSource source, SEAudioBuffer buffer);

        void Play(SEAudioSource source);
        void Pause(SEAudioSource source);
        void Stop(SEAudioSource source);
        SEAudioState GetState(SEAudioSource source);

        /// <summary>一次性提交音源的全部空间参数，避免逐属性往返调用。</summary>
        void SetSourceParams(SEAudioSource source, in SESourceParams parameters);

        /// <summary>仅更新位置与速度，用于每帧的高频同步。</summary>
        void SetSourceTransform(SEAudioSource source, in Vector3D position, in Vector3D velocity);

        #endregion

        #region 监听器与全局设置

        void SetListener(in SEListenerState listener);
        void SetDistanceModel(SEDistanceModel model);

        /// <summary>多普勒效应强度，0 表示禁用。</summary>
        void SetDopplerFactor(float factor);

        /// <summary>声速，影响多普勒频移计算，单位与场景坐标一致。</summary>
        void SetSpeedOfSound(float speed);

        /// <summary>全局主音量。</summary>
        void SetMasterGain(float gain);

        #endregion

        /// <summary>
        /// 每个音频帧调用一次，供后端处理流式解码、回收已播放完的音源等。
        /// </summary>
        void Update(double deltaTime);
    }
}
