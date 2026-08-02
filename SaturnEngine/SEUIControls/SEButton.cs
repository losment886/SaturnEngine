using SaturnEngine.Asset;
using SaturnEngine.Management;
using SaturnEngine.SEInput;
using SaturnEngine.SEUI.Render;

namespace SaturnEngine.SEUIControls
{
    /// <summary>按钮的交互状态。</summary>
    public enum SEButtonState
    {
        Normal = 0,
        Hover = 1,
        Pressed = 2,
    }

    public class SEButton : SEControl
    {
        public Str? Text;

        /// <summary>常态贴图。</summary>
        public SESpirit? NormalImage { get; set; }
        /// <summary>悬停贴图，为空时回退到 <see cref="NormalImage"/>。</summary>
        public SESpirit? HoverImage { get; set; }
        /// <summary>按下贴图，为空时依次回退到 <see cref="HoverImage"/> / <see cref="NormalImage"/>。</summary>
        public SESpirit? PressedImage { get; set; }

        /// <summary>状态切换时播放的动画，由宿主注入 <see cref="SEUIAnimator"/> 与工厂方法。</summary>
        public SEUIAnimator? Animator { get; set; }

        /// <summary>根据新旧状态创建过渡动画；返回 null 表示不播放。</summary>
        public Func<SEButton, SEButtonState, SEButtonState, SEUIAnimation?>? StateAnimationFactory { get; set; }

        /// <summary>当前交互状态。</summary>
        public SEButtonState State { get; private set; } = SEButtonState.Normal;

        /// <summary>状态发生变化时触发。</summary>
        public event Action<SEButton, SEButtonState>? StateChanged;

        public SEButton()
            : base("SEButton")
        {
            Size = new SEMath.Vector2D(400, 100);
            Border = new SEMath.SEMargin(10, 10, 10, 10);
            Bind = SEAnchor.LeftTop;

        }

        private void SetState(SEButtonState state)
        {
            if (State == state)
                return;

            var previous = State;
            State = state;

            // 贴图逐级回退，避免未配置某一态时按钮变空白
            Spirit = state switch
            {
                SEButtonState.Pressed => PressedImage ?? HoverImage ?? NormalImage ?? Spirit,
                SEButtonState.Hover => HoverImage ?? NormalImage ?? Spirit,
                _ => NormalImage ?? Spirit,
            };

            if (Animator is not null && StateAnimationFactory is not null)
            {
                var animation = StateAnimationFactory(this, previous, state);
                if (animation is not null)
                {
                    Animator.StopAll(this);
                    Animator.Play(animation);
                }
            }

            StateChanged?.Invoke(this, state);
        }

        bool isPressed = false;
        bool isPressedRight = false;

        double tc = 0;
        double tcr = 0;

        bool enter = false;
        public override void Update(double deltaTime)
        {
            var s = Position;
            //鼠标处理
            if (s != null && (s.Value.Height == 2 && s.Value[1].Length == 2) && BasicInput.CursorLogicPosition.x >= s?[0][0] && BasicInput.CursorLogicPosition.x <= s?[1][0] && BasicInput.CursorLogicPosition.y >= s?[0][1] && BasicInput.CursorLogicPosition.y <= s?[1][1])
            {
                if(!enter)
                {
                    enter = true;
                    RaiseOnMouseEnter();
                }

                SetState(BasicInput.IfKeyDown(Keys.LButton) ? SEButtonState.Pressed : SEButtonState.Hover);
                
                //SELogger.Log("Position:"+s);
                //鼠标(或虚拟指针)在按钮上
                if (BasicInput.IfKeyDown(Keys.LButton))
                {
                    //按下

                    if (!isPressed)
                    {
                        //OnKeyDown?.Invoke();
                        RaiseOnMouseDown();
                        tc = GetCurrentTime();
                    }
                    isPressed = true;

                }
                else if (isPressed)
                {
                    isPressed = false;
                    //SELogger.Log($"Button {Name} Clicked!! Position:{Position} || Cursor :{BasicInput.CursorLogicPosition}");
                    //OnKeyUp?.Invoke();
                    RaiseOnMouseUp();
                    tc = GetCurrentTime() - tc;
                    if(tc > 0.01 && tc < 0.6)//单击
                    {
                        RaiseOnClick();
                    }
                    tc = 0;
                    
                }
                if (BasicInput.IfKeyDown(Keys.RButton))
                {
                    //按下

                    if (!isPressedRight)
                    {
                        //OnKeyDown?.Invoke();
                        RaiseOnMouseDownRight();
                        tcr = GetCurrentTime();
                    }
                    isPressedRight = true;

                }
                else if (isPressedRight)
                {
                    isPressedRight = false;
                    //SELogger.Log($"Button {Name} Clicked on right!! Position:{Position} || Cursor :{BasicInput.CursorLogicPosition}");
                    //OnKeyUp?.Invoke();
                    RaiseOnMouseUpRight();
                    tcr = GetCurrentTime() - tcr;
                    if(tcr > 0.01 && tcr < 0.6)
                    {
                        RaiseOnClickRight();
                    }
                    tcr = 0;
                }
            }
            else
            {
                if(enter)
                {
                    enter = false;
                    RaiseOnMouseLeave();
                }

                SetState(SEButtonState.Normal);
                if (isPressed)
                {
                    if (!BasicInput.IfKeyDown(Keys.LButton))
                    {
                        //按下
                        isPressed = false;
                    }
                }
                if (isPressedRight)
                {
                    if (!BasicInput.IfKeyDown(Keys.RButton))
                    {
                        //按下
                        isPressedRight = false;
                    }
                }
            }
        }
        Dictionary<Keys,double> kpool = new Dictionary<Keys,double>();
        public override void OnKeyInputEvent(Keys key, bool enable)
        {
            //var s = Position;
            //鼠标处理
            SELogger.Log("Key Event:" + key + " Enable:" + enable);
            if (enter)
            {
                if(enable)
                {
                    RaiseOnKeyDown(key);
                    kpool[key] = GetCurrentTime();
                }
                else
                {
                    if(kpool.ContainsKey(key))
                    {
                        //在MCACOS上按Capslock先触发Capslock，抬起时触发None，需要避免None触发Press
                        RaiseOnKeyUp(key);
                        double c = GetCurrentTime() - kpool[key];
                        kpool[key] = 0;
                        if (c > 0.01 && c < 0.6)//判断加去抖，防止误触发
                        {
                            //SELogger.Log("时长:" + c);
                            RaiseOnKeyPress(key);
                        }
                    }
                }
            }
        }
    }
}
