using SaturnEngine.SEInput;
using SaturnEngine.SEMath;

namespace SaturnEngine.Base
{
    public interface IComponent
    {
        public abstract Asset.SEComponents Components { get; set; }
    }
    public interface ITransform
    {
        public abstract Transform Transform { get; set; }
    }
    public interface IUpdateLoop
    {
        /// <summary>
        /// 更新循环
        /// </summary>
        /// <param name="deltaTime">上一帧的时间间隔</param>
        public abstract void Update(double deltaTime);
    }
    public interface IGetWindowStyle
    {
        public abstract string GetWindowStyle();
    }
    public interface IKeyboardInput
    {
        public abstract void OnKeyDown(Keys key);
        public abstract void OnKeyUp(Keys key);
    }
}
