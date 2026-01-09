using SaturnEngine.Asset;
using SaturnEngine.Global;
using SaturnEngine.SEInput;
using SixLabors.Fonts;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaturnEngine.SEUIControls
{
    public class SELabel : SEControl
    {
        public string Text { get => v; set => SetText(value); }
        string v;
        public SEColor TextColor { get; set; }
        public SEColor BackGroundColor { get; set; }
        public Font TextFont { get; set; }
        public void SetText(string text)
        {
            if(v == text) return;
            v = text;
            if(Spirit == null)
            {
                Spirit = new SESpirit();
                
                Spirit.Load(new SEImageFile());
                var vf = GVariables.FontRenderer.GetTextSize(v, TextFont);
                Spirit.BaseImage.CreateWithColor(vf, BackGroundColor);
                GVariables.FontRenderer.RenderText(Spirit.BaseImage, v, TextFont, TextColor);
                Size = Spirit.BaseImage.Size;
            }
            else
            {
                Spirit.BaseImage?.DisposeImage();
                Spirit.Load(new SEImageFile());
                var vf = GVariables.FontRenderer.GetTextSize(v, TextFont);
                Spirit.BaseImage.CreateWithColor(vf, BackGroundColor);
                GVariables.FontRenderer.RenderText(Spirit.BaseImage, v, TextFont, TextColor);
                Size = Spirit.BaseImage.Size;
            }
        }
        public SELabel()
            :base("SELabel")
        {
            TextColor = SEColor.White;
            TextFont = GVariables.EngineDefaultFont;
            BackGroundColor = SEColor.Gray;
            SetText("label");
        }

        public override void OnKeyInputEvent(Keys key, bool enbale)
        {
            
        }

        public override void Update(float deltaTime)
        {
            
        }
    }
}
