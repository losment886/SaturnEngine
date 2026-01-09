using SaturnEngine.Asset;
using SaturnEngine.SEMath;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaturnEngine.SEFont
{

    public class SEFontRenderer
    {
        private readonly FontCollection _fonts;

        public SEFontRenderer()
        {
            _fonts = new FontCollection();
        }

        // 从文件加载字体
        public Font LoadFontFromFile(string fontPath, float fontSize, FontStyle style = FontStyle.Regular)
        {
            // 加载字体文件
            var family = _fonts.Add(fontPath);
            return family.CreateFont(fontSize, style);
        }

        // 从流加载字体
        public Font LoadFontFromStream(Stream fontStream, float fontSize, FontStyle style = FontStyle.Regular)
        {
            var family = _fonts.Add(fontStream);
            return family.CreateFont(fontSize, style);
        }
        public Vector2D GetTextSize(string text, Font font)
        {
            var textOptions = new TextOptions(font);
            var textSize = TextMeasurer.MeasureSize(text, textOptions);
            return new Vector2D(textSize.Width, textSize.Height);
        }
        public SEImageFile RenderText(string text,Font font,SEColor fillColor)
        {
            
            var textOptions = new TextOptions(font);
            var textSize = TextMeasurer.MeasureSize(text, textOptions);

            // 创建图像（考虑描边宽度）
            var image = new Image<Rgba32>(
                (int)Math.Ceiling(textSize.Width),
                (int)Math.Ceiling(textSize.Height));
            
            image.Mutate(ctx =>
            {
                ctx.DrawText(text,font, Color.FromRgba(fillColor.ToGDIColor().R, fillColor.ToGDIColor().G, fillColor.ToGDIColor().B, fillColor.ToGDIColor().A), new PointF(0,0));
            });
            SEImageFile eif = new SEImageFile();
            eif.BaseImage = image;
            
            return eif;
        }
        // 渲染带描边的文本
        public SEImageFile RenderTextWithOutline(
            string text,
            Font font,
            SEColor fillColor,
            SEColor outlineColor,
            float outlineWidth)
        {
            // 测量文本
            var textOptions = new TextOptions(font);
            var textSize = TextMeasurer.MeasureSize(text, textOptions);

            // 创建图像（考虑描边宽度）
            var image = new Image<Rgba32>(
                (int)Math.Ceiling(textSize.Width + outlineWidth * 2),
                (int)Math.Ceiling(textSize.Height + outlineWidth * 2));

            image.Mutate(ctx =>
            {
                // 先绘制描边（多次偏移绘制来模拟描边）
                for (float x = -outlineWidth; x <= outlineWidth; x += outlineWidth / 2)
                {
                    for (float y = -outlineWidth; y <= outlineWidth; y += outlineWidth / 2)
                    {
                        if (Math.Sqrt(x * x + y * y) <= outlineWidth)
                        {
                            ctx.DrawText(
                                text,
                                font,
                                Color.FromRgba(outlineColor.ToGDIColor().R, outlineColor.ToGDIColor().G, outlineColor.ToGDIColor().B, outlineColor.ToGDIColor().A),
                                new PointF(outlineWidth + x, outlineWidth + y));
                        }
                    }
                }

                // 再绘制填充文本
                ctx.DrawText(
                    text,
                    font,
                    Color.FromRgba(fillColor.ToGDIColor().R, fillColor.ToGDIColor().G, fillColor.ToGDIColor().B, fillColor.ToGDIColor().A),
                    new PointF(outlineWidth, outlineWidth));
            });
            SEImageFile eif = new SEImageFile();
            eif.BaseImage = image;
            return eif;
        }

        // 渲染带阴影的文本
        public SEImageFile RenderTextWithShadow(
            string text,
            Font font,
            SEColor textColor,
            SEColor shadowColor,
            PointF shadowOffset,
            float shadowBlur = 0)
        {
            var textOptions = new TextOptions(font);
            var textSize = TextMeasurer.MeasureSize(text, textOptions);

            // 计算图像尺寸（考虑阴影偏移）
            var padding = Math.Max(Math.Abs(shadowOffset.X), Math.Abs(shadowOffset.Y)) + shadowBlur;
            var image = new Image<Rgba32>(
                (int)Math.Ceiling(textSize.Width + padding * 2),
                (int)Math.Ceiling(textSize.Height + padding * 2));

            image.Mutate(ctx =>
            {
                // 绘制阴影
                if (shadowBlur > 0)
                {
                    // 模糊阴影效果
                    for (int i = 0; i < 3; i++)
                    {
                        var offset = new PointF(
                            shadowOffset.X * (1 - i * 0.1f),
                            shadowOffset.Y * (1 - i * 0.1f));

                        ctx.DrawText(
                            text,
                            font,
                            Color.FromRgba(
                                shadowColor.ToGDIColor().R,
                                shadowColor.ToGDIColor().G,
                                shadowColor.ToGDIColor().B,
                                (byte)(shadowColor.ToGDIColor().A * (0.7f - i * 0.2f))),
                            new PointF(padding + offset.X, padding + offset.Y));
                    }
                }
                else
                {
                    // 简单阴影
                    ctx.DrawText(
                        text,
                        font,
                        Color.FromRgba(
                                shadowColor.ToGDIColor().R,
                                shadowColor.ToGDIColor().G,
                                shadowColor.ToGDIColor().B,
                                shadowColor.ToGDIColor().A),
                        new PointF(padding + shadowOffset.X, padding + shadowOffset.Y));
                }

                // 绘制主文本
                ctx.DrawText(
                    text,
                    font,
                    Color.FromRgba(
                                textColor.ToGDIColor().R,
                                textColor.ToGDIColor().G,
                                textColor.ToGDIColor().B,
                                textColor.ToGDIColor().A),
                    new PointF(padding, padding));
            });
            SEImageFile eif = new SEImageFile();
            eif.BaseImage = image;
            return eif;
        }
        public SEImageFile RenderRichText(
            List<(string Text, Font Font, Color Color)> textParts,
            int maxWidth)
        {
            // 计算总高度
            float totalHeight = 0;
            float currentLineHeight = 0;
            float currentWidth = 0;

            foreach (var part in textParts)
            {
                var size = TextMeasurer.MeasureSize(part.Text, new TextOptions(part.Font));

                if (currentWidth + size.Width > maxWidth)
                {
                    totalHeight += currentLineHeight;
                    currentLineHeight = size.Height;
                    currentWidth = size.Width;
                }
                else
                {
                    currentWidth += size.Width;
                    currentLineHeight = Math.Max(currentLineHeight, size.Height);
                }
            }

            totalHeight += currentLineHeight;

            // 创建图像
            var image = new Image<Rgba32>(maxWidth, (int)Math.Ceiling(totalHeight));

            image.Mutate(ctx =>
            {
                float x = 0, y = 0;
                float lineHeight = 0;

                foreach (var part in textParts)
                {
                    var size = TextMeasurer.MeasureSize(part.Text, new TextOptions(part.Font));

                    // 检查是否需要换行
                    if (x + size.Width > maxWidth && x > 0)
                    {
                        x = 0;
                        y += lineHeight;
                        lineHeight = 0;
                    }

                    // 绘制文本部分
                    ctx.DrawText(part.Text, part.Font, part.Color, new PointF(x, y));

                    x += size.Width;
                    lineHeight = Math.Max(lineHeight, size.Height);
                }
            });
            SEImageFile eif = new SEImageFile();
            eif.BaseImage = image;
            return eif;
        }


        public void RenderText(SEImageFile image, string text, Font font, SEColor fillColor)
        {
            image.BaseImage.Mutate(ctx =>
            {
                ctx.DrawText(text, font, Color.FromRgba(fillColor.ToGDIColor().R, fillColor.ToGDIColor().G, fillColor.ToGDIColor().B, fillColor.ToGDIColor().A), new PointF(0, 0));
            });
            //image.SaveImageToPNGFile("E:\\sc.png");
        }
        // 渲染带描边的文本
        public void RenderTextWithOutline(
            SEImageFile image,
            string text,
            Font font,
            SEColor fillColor,
            SEColor outlineColor,
            float outlineWidth)
        {
            if (!image.IsLoaded)
                return;
            // 测量文本
            image.BaseImage.Mutate(ctx =>
            {
                // 先绘制描边（多次偏移绘制来模拟描边）
                for (float x = -outlineWidth; x <= outlineWidth; x += outlineWidth / 2)
                {
                    for (float y = -outlineWidth; y <= outlineWidth; y += outlineWidth / 2)
                    {
                        if (Math.Sqrt(x * x + y * y) <= outlineWidth)
                        {
                            ctx.DrawText(
                                text,
                                font,
                                Color.FromRgba(outlineColor.ToGDIColor().R, outlineColor.ToGDIColor().G, outlineColor.ToGDIColor().B, outlineColor.ToGDIColor().A),
                                new PointF(outlineWidth + x, outlineWidth + y));
                        }
                    }
                }

                // 再绘制填充文本
                ctx.DrawText(
                    text,
                    font,
                    Color.FromRgba(fillColor.ToGDIColor().R, fillColor.ToGDIColor().G, fillColor.ToGDIColor().B, fillColor.ToGDIColor().A),
                    new PointF(outlineWidth, outlineWidth));
            });

        }

        // 渲染带阴影的文本
        public void RenderTextWithShadow(
            SEImageFile image,
            string text,
            Font font,
            SEColor textColor,
            SEColor shadowColor,
            PointF shadowOffset,
            float shadowBlur = 0)
        {
            if (!image.IsLoaded)
                return;
            var textOptions = new TextOptions(font);
            var textSize = TextMeasurer.MeasureSize(text, textOptions);

            // 计算图像尺寸（考虑阴影偏移）
            var padding = Math.Max(Math.Abs(shadowOffset.X), Math.Abs(shadowOffset.Y)) + shadowBlur;

            image.BaseImage.Mutate(ctx =>
            {
                // 绘制阴影
                if (shadowBlur > 0)
                {
                    // 模糊阴影效果
                    for (int i = 0; i < 3; i++)
                    {
                        var offset = new PointF(
                            shadowOffset.X * (1 - i * 0.1f),
                            shadowOffset.Y * (1 - i * 0.1f));

                        ctx.DrawText(
                            text,
                            font,
                            Color.FromRgba(
                                shadowColor.ToGDIColor().R,
                                shadowColor.ToGDIColor().G,
                                shadowColor.ToGDIColor().B,
                                (byte)(shadowColor.ToGDIColor().A * (0.7f - i * 0.2f))),
                            new PointF(padding + offset.X, padding + offset.Y));
                    }
                }
                else
                {
                    // 简单阴影
                    ctx.DrawText(
                        text,
                        font,
                        Color.FromRgba(
                                shadowColor.ToGDIColor().R,
                                shadowColor.ToGDIColor().G,
                                shadowColor.ToGDIColor().B,
                                shadowColor.ToGDIColor().A),
                        new PointF(padding + shadowOffset.X, padding + shadowOffset.Y));
                }

                // 绘制主文本
                ctx.DrawText(
                    text,
                    font,
                    Color.FromRgba(
                                textColor.ToGDIColor().R,
                                textColor.ToGDIColor().G,
                                textColor.ToGDIColor().B,
                                textColor.ToGDIColor().A),
                    new PointF(padding, padding));
            });

        }
        public void RenderRichText(
            SEImageFile image,
            List<(string Text, Font Font, Color Color)> textParts,
            int maxWidth)
        {
            if (!image.IsLoaded)
                return;
            // 计算总高度
            float totalHeight = 0;
            float currentLineHeight = 0;
            float currentWidth = 0;

            foreach (var part in textParts)
            {
                var size = TextMeasurer.MeasureSize(part.Text, new TextOptions(part.Font));

                if (currentWidth + size.Width > maxWidth)
                {
                    totalHeight += currentLineHeight;
                    currentLineHeight = size.Height;
                    currentWidth = size.Width;
                }
                else
                {
                    currentWidth += size.Width;
                    currentLineHeight = Math.Max(currentLineHeight, size.Height);
                }
            }

            totalHeight += currentLineHeight;

            // 创建图像
            

            image.BaseImage.Mutate(ctx =>
            {
                float x = 0, y = 0;
                float lineHeight = 0;

                foreach (var part in textParts)
                {
                    var size = TextMeasurer.MeasureSize(part.Text, new TextOptions(part.Font));

                    // 检查是否需要换行
                    if (x + size.Width > maxWidth && x > 0)
                    {
                        x = 0;
                        y += lineHeight;
                        lineHeight = 0;
                    }

                    // 绘制文本部分
                    ctx.DrawText(part.Text, part.Font, part.Color, new PointF(x, y));

                    x += size.Width;
                    lineHeight = Math.Max(lineHeight, size.Height);
                }
            });
        }
    }
}
