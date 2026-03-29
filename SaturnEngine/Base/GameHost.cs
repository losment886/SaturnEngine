using SaturnEngine.Asset;
using SaturnEngine.Global;
using SaturnEngine.Management;
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
        public List<Game> Games;
        public List<bool> Loaded;
        public SEThread MainThread;
        
        public GameHost()
        {
            Platform.Global.EngineInit();
            Games = new List<Game>();
            Loaded = new List<bool>();
        }

        public void LoadGame(Game g)
        {
            Games.Add(g);
            Loaded.Add(false);
        }

        public void DestoryGame(string name)
        {
            Game? g = null;
            for (int i = 0; i < Games.Count; i++)
            {
                if (Games[i].Name == name)
                {
                    g = Games[i];
                    break;
                }
            }
            if (g == null)
            {
                SELogger.Error($"未找到名为{name}的游戏".GetInCurrLang(), "GameHost");
                return;
            }
            else
            {
                int index = Games.IndexOf(g);
                Games.RemoveAt(index);
                Loaded.RemoveAt(index);
                g.Exit();
            }
        }

        public void LaunchGame(string name)
        {
            if (Dispatcher.CheckMainThread())
            {
                Game? g = null;
                for (int i = 0; i < Games.Count; i++)
                {
                    if (Games[i].Name == name)
                    {
                        g = Games[i];
                        break;
                    }
                }

                if (g == null)
                {
                    SELogger.Error($"未找到名为{name}的游戏".GetInCurrLang(), "GameHost");
                    return;
                }
                else
                {
                    int index = Games.IndexOf(g);
                    if (Loaded[index] == false)
                    {
                        g.ThisWindow.Initialize();
                        g.ThisWindow.CreateWindow();
                        g.ThisWindow.RunWindow();
                        Loaded[index] = true;
                    }
                    else
                    {
                        SELogger.Warn("游戏已启动，无法再次启动".GetInCurrLang(), "GameHost");
                    }
                }
            }
            else
            {
                SELogger.Warn("不在主线程中调用LaunchGame可能会导致未知错误，将添加至下一周期的主函数事件中".GetInCurrLang(), "GameHost");
                MainThreadQueue.Add(() =>
                {
                    Game? g = null;
                    for (int i = 0; i < Games.Count; i++)
                    {
                        if (Games[i].Name == name)
                        {
                            g = Games[i];
                            break;
                        }
                    }

                    if (g == null)
                    {
                        SELogger.Error($"未找到名为{name}的游戏".GetInCurrLang(), "GameHost");
                        return;
                    }
                    else
                    {
                        int index = Games.IndexOf(g);
                        if (Loaded[index] == false)
                        {
                            g.ThisWindow.Initialize();
                            g.ThisWindow.CreateWindow();
                            g.ThisWindow.RunWindow();
                            Loaded[index] = true;
                        }
                        else
                        {
                            SELogger.Warn("游戏已启动，无法再次启动".GetInCurrLang(), "GameHost");
                        }
                    }
                });
            }
        }
        /// <summary>
        /// 启动游戏主机，默认运行最先添加的Game对象，其他对象需要使用LaunchGame方法启动
        /// </summary>
        public void Start()
        {
            //SW.RunWindow();

            MainThread = Dispatcher.CreateThreadFromExistedThread();
            MainThreadWorker();
        }
        void MainThreadWorker()
        {
            LaunchGame(Games[0].Name);
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
