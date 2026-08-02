using SaturnEngine.Asset;
using SaturnEngine.SEInput;

namespace SaturnEngine.SEUIControls
{
    /// <summary>
    /// 容器控件：提供背景色 / 背景图，并可裁剪超出自身范围的子控件。
    /// </summary>
    public class SEPanel : SEControl
    {
        private SEColor _backgroundColor;

        public SEPanel()
            : base("SEPanel")
        {
            _backgroundColor = SEColor.Transparent;
            Tint = _backgroundColor;
            ClipChildren = true;
        }

        /// <summary>
        /// 面板背景色。没有背景图时渲染器用白像素 + 该颜色填充整块区域。
        /// </summary>
        public SEColor BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                Tint = value;
            }
        }

        /// <summary>背景图。设置后渲染器改用图集贴图并以 <see cref="BackgroundColor"/> 作为着色。</summary>
        public SESpirit? BackgroundImage
        {
            get => Spirit;
            set => Spirit = value;
        }

        /// <summary>是否把子控件裁剪在面板范围内，默认开启。</summary>
        public bool ClipChildren { get; set; }

        public override void OnKeyInputEvent(Keys key, bool enbale)
        {

        }

        public override void Update(double deltaTime)
        {
            // 面板自身无交互逻辑，子控件由布局与渲染层递归处理
        }
    }
}
