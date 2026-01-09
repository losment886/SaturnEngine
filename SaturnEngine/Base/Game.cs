using SaturnEngine.Asset;
using SaturnEngine.Global;

namespace SaturnEngine.Base
{
    /// <summary>
    /// SCENE的承载体，用于提交给GAMEHOST
    /// </summary>
    public abstract class Game : SEBase, IUpdateLoop
    {
        public List<Scene> ThisScenes { get; private set; }
        public List<ulong> ThisSTCs { get; private set; }
        public int CurrentSceneIndex { get; private set; } = -1;
        public UIScene? UIScene { get; set; } = null;//UI场景，有就用，没有就算了
        /// <summary>
        /// 添加场景
        /// </summary>
        /// <param name="scene"></param>
        public void AddScene(Scene scene)
        {
            if (ThisScenes == null)
            {
                ThisScenes = new List<Scene>();
            }
            if (ThisSTCs == null)
            {
                ThisSTCs = new List<ulong>();
            }

            ThisScenes.Add(scene);
            ThisSTCs.Add(scene.STC);
        }
        public void LoadScene(int index)
        {
            if (ThisScenes == null || index < 0 || index >= ThisScenes.Count)
            {
                throw new IndexOutOfRangeException("无效的Scene序列".GetInCurrLang());
            }
            if (CurrentSceneIndex >= 0 && CurrentSceneIndex < ThisScenes.Count)
            {
                ThisScenes[CurrentSceneIndex].Leave();
            }
            CurrentSceneIndex = index;
            if (ThisScenes[CurrentSceneIndex].IsLoaded == false)
            {
                ThisScenes[CurrentSceneIndex].Load();
            }
            else
            {
                ThisScenes[CurrentSceneIndex].Activity();
            }
        }
        public void LoadScene(ulong stc)
        {
            int i = ThisSTCs.IndexOf(stc);
            if (i < 0 || i >= ThisScenes.Count)
            {
                throw new Exception("无效的Scene STC码".GetInCurrLang());
            }
            if (CurrentSceneIndex >= 0 && CurrentSceneIndex < ThisScenes.Count)
            {
                ThisScenes[CurrentSceneIndex].Leave();
            }
            CurrentSceneIndex = i;
            if (ThisScenes[CurrentSceneIndex].IsLoaded == false)
            {
                ThisScenes[CurrentSceneIndex].Load();
            }
            else
            {
                ThisScenes[CurrentSceneIndex].Activity();
            }
            GVariables.MainWindow?.Renderer?.SetScene(CurrentSceneIndex);
        }
        /// <summary>
        /// 初始化
        /// </summary>
        /// <remarks>
        /// 在函数退出前请确保至少加载了一个SCENE
        /// </remarks>
        public abstract void Initialize();
        /// <summary>
        /// 游戏退出时触发
        /// </summary>
        public abstract void Exit();
        /// <summary>
        /// 获取焦点时触发
        /// </summary>
        /// <remarks>
        /// 在程序运行的一开始也会判断是否有焦点，请注意判断逻辑。
        /// </remarks>
        public abstract void OnFocus();
        /// <summary>
        /// 失去焦点时触发
        /// </summary>
        /// <remarks>
        /// 在程序运行的一开始也会判断是否有焦点，请注意判断逻辑。
        /// </remarks>
        public abstract void OnLeave();
        /// <summary>
        /// 主线程更新循环
        /// </summary>
        /// <param name="deltaTime"></param>
        public abstract void OnUpdate(float deltaTime);
        public void Update(float deltaTime)
        {
            OnUpdate(deltaTime);
            if (CurrentSceneIndex >= 0)
            {
                ThisScenes[CurrentSceneIndex].Update(deltaTime);
            }
        }
    }
}
