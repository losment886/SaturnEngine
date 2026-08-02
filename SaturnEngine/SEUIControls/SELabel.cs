using SaturnEngine.Asset;
using SaturnEngine.Global;
using SaturnEngine.SEInput;
using SaturnEngine.SEUI.Render;
using SixLabors.Fonts;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaturnEngine.SEUIControls
{
    /// <summary>
    /// 文本标签。优先走 <see cref="SEUIFont"/> 图集路径（字形四边形由渲染器直接生成，
    /// 无需为每次文本变化重建位图）；未提供 <see cref="UIFont"/> 时回退到基于
    /// <see cref="SixLabors.Fonts"/> 的离屏位图渲染。
    /// </summary>
    public class SELabel : SEControl, ISEUITextDrawable
    {
        public string Text { get => v; set => SetText(value); }
        string v;
        public SEColor TextColor { get; set; }
        public SEColor BackGroundColor { get; set; }
        public Font TextFont { get; set; }

        private SEUIFont? _uiFont;

        /// <summary>图集字体。赋值后启用新的字形渲染路径。</summary>
        public SEUIFont? UIFont
        {
            get => _uiFont;
            set
            {
                _uiFont = value;
                // 切换到图集路径后旧位图不再需要，及时释放
                if (value is not null)
                {
                    Spirit?.BaseImage?.DisposeImage();
                    Spirit = null;
                }
                Remeasure();
            }
        }

        /// <summary>当前是否使用图集字体路径。</summary>
        public bool UsesAtlasFont => _uiFont is not null;

        public void SetText(string text)
        {
            if(v == text) return;
            v = text;
            Remeasure();
        }

        private void Remeasure()
        {
            if (string.IsNullOrEmpty(v))
                return;

            if (_uiFont is not null)
            {
                // 图集路径只需要尺寸，绘制交给 SEUIRenderer.AddText
                var (width, height) = _uiFont.Measure(v);
                Size = new SEMath.Vector2D(width, height);
                return;
            }

            RenderToBitmap();
        }

        private void RenderToBitmap()
        {
            Spirit?.BaseImage?.DisposeImage();
            Spirit ??= new SESpirit();

            Spirit.Load(new SEImageFile());
            var vf = GVariables.FontRenderer.GetTextSize(v, TextFont);
            Spirit.BaseImage.CreateWithColor(vf, BackGroundColor);
            GVariables.FontRenderer.RenderText(Spirit.BaseImage, v, TextFont, TextColor);
            Size = Spirit.BaseImage.Size;
        }

        public SELabel()
            :base("SELabel")
        {
            TextColor = SEColor.White;
            TextFont = GVariables.EngineDefaultFont;
            BackGroundColor = SEColor.Gray;
            SetText("label");
        }

        /// <summary>
        /// 图集路径下把文本追加到绘制列表；位图路径下由控件自身的 Spirit 绘制。
        /// </summary>
        public void Draw(SEUIRenderer renderer)
        {
            ArgumentNullException.ThrowIfNull(renderer);

            if (_uiFont is null || Position is null || string.IsNullOrEmpty(v))
                return;

            var pos = Position.Value;
            renderer.AddText(v, (float)pos[0][0], (float)pos[0][1], TextColor, GetEffectiveOpacity(), _uiFont);
        }

        public override void OnKeyInputEvent(Keys key, bool enbale)
        {

        }

        public override void Update(double deltaTime)
        {

        }
    }
}
