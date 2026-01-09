using SaturnEngine.Base;

namespace SaturnEngine.SEAudio
{
    public abstract class SEAudioProvider : SEBase
    {
        public SEAudioProvider() { }

        public abstract bool IsPlaying { get; }
        public abstract void Play(int id);
        public abstract void Stop(int id);
        public abstract void Pause(int id);
        public abstract void CreateDevice();
        public abstract void UpdateDevice();
        public abstract void UpdateAudio();
        public abstract void Init();

    }
}
