using SaturnEngine.Base;
using SaturnEngine.Physics;

namespace SaturnEngine.Asset
{
    public abstract class Scene : SEBase, IUpdateLoop
    {
        public bool IsLoaded { get; private set; } = false;
        public List<GameObject> ThisGameObjects { get; private set; } = new List<GameObject>();
        public TrigManager ThisTrigManager { get; private set; } = new TrigManager();
        public Scene(string nm = "scene", string desc = "NULL")
            : base(nm, desc)
        {

        }
        public void Load()
        {
            OnLoad();
            IsLoaded = true;
        }
        public void Activity()
        {
            OnActivity();
        }
        public void Leave()
        {
            OnLeave();
        }
        public void Exit()
        {
            OnExit();
            IsLoaded = false;
        }
        public abstract void OnLoad();//第一次加载场景时调用
        public abstract void OnActivity();//在切换到此场景调用（第一次不调用，仅在退出场景后再次进入时调用）
        public abstract void OnLeave();//在切换到其他场景时调用
        public abstract void OnExit();//在退出场景时调用（不保留场景）
        public abstract void Update(double deltaTime);//在主线程的更新
        public virtual void EXTUpdate(float deltaTime)//在额外事件线程的更新，频率一般高于主线程,可选继承,用以奉送音频视频贴图等资源以及高速事件反馈
        {

        }
        public virtual void Render(float deltaTime)//渲染,可覆写
        {

        }
    }
}
