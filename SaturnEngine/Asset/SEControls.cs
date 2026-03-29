using SaturnEngine.Base;
using SaturnEngine.SEInput;
using SaturnEngine.SEMath;

namespace SaturnEngine.Asset
{
    public class SEControls : SEBase, IUpdateLoop
    {
        public List<SEControl> Controls = new List<SEControl>();
        public void Init()
        {
            for (int i = 0; i < Controls.Count; i++)
            {
                Controls[i].Init();
            }
        }
        //public bool[] Used;
        /// <summary>
        /// 计算位置
        /// </summary>
        public void Flush(Vector2D WindowSize)
        {

            FM(Controls.ToArray(), new SERect([[0, 0], [WindowSize.X, WindowSize.Y]]));
        }
        /// <summary>
        /// 递归布局函数 - 计算控件位置
        /// </summary>
        /// <param name="curr">当前控件数组</param>
        /// <param name="frange">父级范围</param>
        /// <param name="allowh">是否允许水平布局</param>
        private void FM(SEControl[] curr, SERect frange, bool allowh = false)
        {
            //Vector2D currentPos = new Vector2D(frange[0][0], frange[0][1]); // 当前布局位置LT
            //Vector2D currentPosR = new Vector2D(frange[1][0], frange[0][1]); // 当前布局位置RT
            double currTopY = frange[0][1];
            double currBottomY = frange[1][1];
            double currLeftTX = frange[0][0];
            double currRightTX = frange[1][0];
            double currLeftBX = frange[0][0];
            double currRightBX = frange[1][0];
            //double maxXInRow = 0; // 当前行最大X
            //double maxYInCol = 0; // 当前列最大Y

            for (int i = 0; i < curr.Length; i++)
            {
                var c = curr[i];

                Vector2D controlSize = c.Size;
                SERect controlRect = new SERect([new Vector2D(0, 0), new Vector2D(0, 0)]);

                // 根据绑定方式计算位置
                switch (c.Bind)
                {
                    default:
                    case SEAnchor.LeftTop:
                        if (allowh)
                        {
                            controlRect = new SERect([[currLeftTX + c.Border.Left, currTopY + c.Border.Top], [currLeftTX + c.Border.Left + c.Size.X, currTopY + c.Border.Top + c.Size.Y]]);
                            currLeftTX += c.Border.Left + c.Size.X + c.Border.Right;

                        }
                        else
                        {
                            controlRect = new SERect([[currLeftTX + c.Border.Left, currTopY + c.Border.Top], [currLeftTX + c.Border.Left + c.Size.X, currTopY + c.Border.Top + c.Size.Y]]);
                            currTopY += c.Border.Top + c.Size.Y + c.Border.Bottom;
                        }
                        break;

                    case SEAnchor.RightTop:
                        if (allowh)
                        {
                            controlRect = new SERect([[currRightTX - c.Border.Right - c.Size.X, currTopY + c.Border.Top], [currRightTX - c.Border.Right, currTopY + c.Border.Top + c.Size.Y]]);
                            currRightTX -= c.Border.Left + c.Size.X + c.Border.Right;

                        }
                        else
                        {
                            controlRect = new SERect([[currRightTX - c.Border.Right - c.Size.X, currTopY + c.Border.Top], [currRightTX - c.Border.Right, currTopY + c.Border.Top + c.Size.Y]]);
                            currTopY += c.Border.Top + c.Size.Y + c.Border.Bottom;
                        }
                        break;

                    case SEAnchor.LeftBottom:
                        if (allowh)
                        {
                            controlRect = new SERect([[currLeftBX + c.Border.Left, currBottomY - c.Border.Bottom - c.Size.Y], [currLeftBX + c.Border.Left + c.Size.X, currBottomY - c.Border.Bottom]]);
                            currLeftBX += c.Border.Left + c.Size.X + c.Border.Right;

                        }
                        else
                        {
                            controlRect = new SERect([[currLeftBX + c.Border.Left, currBottomY - c.Border.Bottom - c.Size.Y], [currLeftBX + c.Border.Left + c.Size.X, currBottomY - c.Border.Bottom]]);
                            currBottomY -= c.Border.Top + c.Size.Y + c.Border.Bottom;
                        }
                        break;

                    case SEAnchor.RightBottom:
                        if (allowh)
                        {
                            controlRect = new SERect([[currRightBX - c.Border.Right - c.Size.X, currBottomY - c.Border.Bottom - c.Size.Y], [currRightBX - c.Border.Right, currBottomY - c.Border.Bottom]]);
                            currRightBX -= c.Border.Left + c.Size.X + c.Border.Right;

                        }
                        else
                        {
                            controlRect = new SERect([[currRightBX - c.Border.Right - c.Size.X, currBottomY - c.Border.Bottom - c.Size.Y], [currRightBX - c.Border.Right, currBottomY - c.Border.Bottom]]);
                            currBottomY -= c.Border.Top + c.Size.Y + c.Border.Bottom;
                        }
                        break;


                }

                c.Position = controlRect;

                // 递归处理子控件
                if (c.Child != null && c.Child.Length > 0)
                {
                    // 使用当前控件的位置作为子控件的父范围
                    FM(c.Child, c.Position ?? frange, c.AllowHorizontalLayout);
                }
            }
        }
        /*
        //递归处理
        private void FM(SEControl[] curr,SERect frange, bool allowh = false)
        {
            //SERect s = new SERect([[0, 0], [0, 0]]);
            Vector2D v = new Vector2D();//当前相对父范围坐标(左上角)
            double maxX = 0;//同一行下占地
            double maxY = 0;//同一列下占地
            for(int i = 0; i < curr.Length;i++)
            {
                var c = curr[i];
                if(c.Position == null)//为空则说明没处理，就计算
                {
                    switch(c.Bind)
                    {
                        case BindBorder.Left:

                            break;
                    }
                        
                }
                if (c.Child != null)
                    FM(c.Child, c.Position ?? frange, c.AllowHorizontalLayout);
            }
        }*/

        public void Update(double deltaTime)
        {
            for (int i = 0; i < Controls.Count; i++)
            {
                //Controls[i].Update(deltaTime);
                UPD(Controls[i], deltaTime);
            }
        }
        void UPD(SEControl ctrl,double deltaTime)
        {
            ctrl.Update(deltaTime);
            if (ctrl.Child != null)
            {
                for (int i = 0; i < ctrl.Child.Length; i++)
                {
                    UPD(ctrl.Child[i], deltaTime);
                }
            }
        }
        public SEControls(string nm = "controls", string desc = "NULL")
            : base(nm, desc)
        {
        }
        public void Add(SEControl control)
        {
            Controls.Add(control);
            //Positions.Add(new Vector2D(0, 0));
        }
        public void Remove(SEControl control)
        {

            //Positions.RemoveAt(Controls.IndexOf(control));
            Controls.Remove(control);
        }
        int GCC(SEControl control)
        {
            int cnt = 0;
            if (control.Child != null)
            {
                cnt += control.Child.Length;
                for (int i = 0; i < control.Child.Length; i++)
                {
                    cnt += GCC(control.Child[i]);
                }
            }
            return cnt;
        }
        public int Count()
        {
            int co = 0;
            for(int i = 0; i < Controls.Count; i++)
            {
                co++;
                co += GCC(Controls[i]);
            }
            return co;
        }
        public SEControl this[int index]
        {
            get { return Controls[index]; }
        }

    }
    public enum SEAnchor : int
    {


        LeftTop = 4,
        RightTop = 5,
        LeftBottom = 6,
        RightBottom = 7,


    }
    public abstract class SEControl : SEBase, IUpdateLoop
    {
        public SEControl(string nm = "control", string desc = "NULL")
            : base(nm, desc)
        {
            Size = new Vector2D(0, 0);
            Border = new SEMargin();
            Bind = SEAnchor.LeftTop;
            AllowHorizontalLayout = false;

            BasicInput.OnKeyInput += OnKeyInputEvent;
        }
        public SESpirit? Spirit;
        public SEControl? Parent;
        public SEControl[]? Child;
        public Vector2D Size;//控件大小
        public SEMargin Border;//相对于上一个界面的位置，如果没有上一个界面则相对于屏幕，与4周的间距，Left,Top,Right,Bottom，
        public SEAnchor Bind;//绑定位置，默认左上角，
        public bool AllowHorizontalLayout;//是否允许子控件水平布局，否则垂直布局，默认false，仅针对子控件，在横向排布时，超出宽度则换行，在纵向排布时，超出高度则换列
        public SERect? Position;//由布局引擎设置，表示相对渲染窗口的绝对位置，左上与右下
        public double Angle;//渲染图元旋转角
        public abstract void Update(double deltaTime);
        public abstract void OnKeyInputEvent(Keys key,bool enbale);

        public delegate void PVoid();
        public delegate void PKeys(Keys s);
        public delegate void PInit(SEControl t);

        protected void RaiseOnMouseEnter()
        {
            OnMouseEnter?.Invoke();
        }
        protected void RaiseOnMouseLeave()
        {
            OnMouseLeave?.Invoke();
        }
        protected void RaiseOnMouseMove()
        {
            OnMouseMove?.Invoke();
        }
        protected void RaiseOnMouseUp()
        {
            OnMouseUp?.Invoke();
        }
        protected void RaiseOnMouseWheel()
        {
            OnMouseWheel?.Invoke();
        }
        protected void RaiseOnMouseDown()
        {
            OnMouseDown?.Invoke();
        }
        protected void RaiseOnKeyDown(Keys s)
        {
            OnKeyDown?.Invoke(s);
        }
        protected void RaiseOnKeyUp(Keys s)
        {
            OnKeyUp?.Invoke(s);
        }
        protected void RaiseOnKeyPress(Keys s)
        {
            OnKeyPress?.Invoke(s);
        }
        protected void RaiseOnClick()
        {
            OnClick?.Invoke();
        }
        protected void RaiseOnMouseUpRight()
        {
            OnMouseUpRight?.Invoke();
        }
        protected void RaiseOnMouseDownRight()
        {
            OnMouseDownRight?.Invoke();
        }
        protected void RaiseOnClickRight()
        {
            OnClickRight?.Invoke();
        }
        protected void RaiseOnInit()
        {
            OnInit?.Invoke(this);
        }
        public void Init()
        {
            RaiseOnInit();
        }

        public event PVoid? OnMouseEnter;
        public event PVoid? OnMouseLeave;
        public event PVoid? OnMouseMove;
        public event PVoid? OnMouseWheel;
        public event PVoid? OnMouseUp;
        public event PVoid? OnMouseDown;
        public event PVoid? OnClick;
        public event PVoid? OnMouseUpRight;
        public event PVoid? OnMouseDownRight;
        public event PVoid? OnClickRight;

        public event PKeys? OnKeyDown;
        public event PKeys? OnKeyUp;
        public event PKeys? OnKeyPress;

        public event PInit? OnInit;
    }
}
