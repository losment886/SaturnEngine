using SaturnEngine.Asset;
using SaturnEngine.Global;
using SaturnEngine.Performance;
using SaturnEngine.SEGraphics;

namespace SaturnEngine.Base
{
    /// <summary>
    /// 帮助加载GAME，以及高度封装引擎代码
    /// </summary>
    public class GameHost : SEBase
    {
        //public SEWindow SW { get; private set; } = null!;
        //public Game G { get; private set; } = null!;
        public SEThread MainThread;
        public GameHost()
        {
            Platform.Global.EngineInit();
        }

        public void LoadGame(Game g)
        {
            
        }
        public void Start()
        {
            //SW.RunWindow();

            MainThread = Dispatcher.CreateThreadFromExistedThread();
            MainThreadWorker();
        }
        void MainThreadWorker()
        {
            MainThread.SetFPS(10000);
            while (true) 
            {
                //包含窗口刷新的队列
                MainThreadQueue.ProcessEvent();
                MainThreadQueue.InvokeAll();
                
                
                
                
                MainThread.WaitForFPS();
            }

        }
    }
}
