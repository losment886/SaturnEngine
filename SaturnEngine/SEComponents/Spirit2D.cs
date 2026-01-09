using SaturnEngine.Asset;
using SaturnEngine.SEMath;

namespace SaturnEngine.SEComponents
{
    /// <summary>
    /// 2D精灵组件，用于显示2D图像
    /// </summary>
    public class Spirit2D : SEComponent
    {
        public SESpirit? BaseSpirit { get; private set; } = null;
        public bool IsLoaded { get { return BaseSpirit?.IsLoaded ?? false; } }
        public SERect MainWindow { get; private set; }//主要显示区域，指向图元窗口
        public SERect SplitSize { get; private set; }//分割区域
        public SERect DrawSize { get; private set; }//绘制区域，在窗口上的大小,不影响MainWindow,仅影响绘制大小,变相指示GameObject大小
        public double Angle { get; set; } = 0;//旋转角度，仅影响窗口到绘制的旋转
        public int CurrentFrame { get; private set; } = 0;//当前帧
        public int TotalFrames { get; private set; } = 0;//总帧数
        public bool Changed { get; set; } = false;//是否更改了显示区域，用于优化，减少不必要的渲染
        public Spirit2D()
        {
            CType = SEComponentType.Spirit2D;
        }

        public void Load(SESpirit? sep, SERect? splitsize)
        {
            BaseSpirit = sep;
            SplitSize = splitsize ?? new SERect([[128, 128]]);
            //MainWindow = new SERect([[0, 0], [SplitSize.Width, SplitSize.Height]]);
            if (IsLoaded)
            {
                TotalFrames = (int)(BaseSpirit.BaseImage.Size.X / SplitSize.Width) * (int)(BaseSpirit.BaseImage.Size.Y / SplitSize.Height);
                SetFrame(0);
            }
        }
        public void Reflash()
        {
            if (IsLoaded)
            {
                TotalFrames = (int)(BaseSpirit.BaseImage.Size.X / SplitSize.Width) * (int)(BaseSpirit.BaseImage.Size.Y / SplitSize.Height);
                SetFrame(CurrentFrame);
            }
        }
        public void SetFrame(int frame)
        {
            if (frame < 0 || frame >= TotalFrames)
            {
                frame = 0;
            }
            CurrentFrame = frame;
            int x = (CurrentFrame * (int)SplitSize.Width) % (int)BaseSpirit.BaseImage.Size.X;
            int y = (CurrentFrame * (int)SplitSize.Width) / (int)BaseSpirit.BaseImage.Size.X * (int)SplitSize.Height;
            MainWindow = new SERect([[x, y], [x + SplitSize.Width, y + SplitSize.Height]]);
            Changed = true;
        }
        public void NextFrame()
        {
            CurrentFrame++;
            if (CurrentFrame >= TotalFrames)
            {
                CurrentFrame = 0;
            }
            int x = (CurrentFrame * (int)SplitSize.Width) % (int)BaseSpirit.BaseImage.Size.X;
            int y = (CurrentFrame * (int)SplitSize.Width) / (int)BaseSpirit.BaseImage.Size.X * (int)SplitSize.Height;
            MainWindow = new SERect([[x, y], [x + SplitSize.Width, y + SplitSize.Height]]);
            Changed = true;
        }
    }
}
