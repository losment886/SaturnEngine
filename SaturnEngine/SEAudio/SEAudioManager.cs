
using SaturnEngine.Base;
using SaturnEngine.Global;

namespace SaturnEngine.SEAudio
{
    public class SEAudioManager : SEBase, IDisposable
    {
        public class SEChannel
        {
            public int id;

            public SEChannel()
            {

            }
        }
        public bool IsEnable { get; private set; } = false;
        public int Handle { get; private set; } = 0;
        public int DefaultFrequency { get; private set; } = 44100;
        public int DefaultChannelCount { get; private set; } = 2;//可能有5.1 7.1等
        public List<int> MixStreams { get; private set; } = new List<int>();
        public SEAudioManager()
        {

        }
        public void Initialize()
        {
            //ManagedBass.Bass.Init(Frequency: DefaultFrequency, Flags: ManagedBass.DeviceInitFlags.Default);
            //ManagedBass.Bass.Apply3D();

        }
        //public void CreateDevice() => GVariables.MainWindow.AudioDel.Add(createDevice);
        private void createDevice()
        {
            
            IsEnable = false;
        }
        //public void AddChannel(string fp, SEChannel s) => GVariables.MainWindow.AudioDel.Add(() => addChannel(fp, s));
        private void addChannel(string fp, SEChannel s)
        {
            
        }
        //public void RemoveChannel(SEChannel s) => GVariables.MainWindow.AudioDel.Add(() => removeChannel(s));
        private void removeChannel(SEChannel s)
        {
            
        }
        //public void PlayChannel(SEChannel s) => GVariables.MainWindow.AudioDel.Add(() => playChannel(s));
        private void playChannel(SEChannel s)
        {
            //Bass.ChannelPlay(MixStreams[s.id]);
        }
        //public void StopChannel(SEChannel s) => GVariables.MainWindow.AudioDel.Add(() => stopChannel(s));
        private void stopChannel(SEChannel s)
        {
            //Bass.ChannelStop(MixStreams[s.id]);
        }
        //public void PauseChannel(SEChannel s) => GVariables.MainWindow.AudioDel.Add(() => pauseChannel(s));
        private void pauseChannel(SEChannel s)
        {
            //Bass.ChannelPause(MixStreams[s.id]);
        }
        //public void SetVolumeChannel(SEChannel s, float volume) => GVariables.MainWindow.AudioDel.Add(() => setVolumeChannel(s, volume));
        private void setVolumeChannel(SEChannel s, float volume)
        {
            //Bass.ChannelSetAttribute(MixStreams[s.id], ChannelAttribute.Volume, volume);
        }

        public void Dispose()
        {
            try
            {
                for (int i = 0; i < MixStreams.Count; i++)
                {
                    //BassMix.MixerRemoveChannel(MixStreams[i]);
                    //Bass.StreamFree(MixStreams[i]);
                }
            }
            catch
            {

            }
        }
    }
}
