using SaturnEngine.Asset;
using SaturnEngine.SEMath;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Text;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SaturnEngine.SEFont
{

    public class SEFontRenderer
    {
        private readonly FontCollection _fonts;

        private static Color ToImageSharpColor(SEColor color)
        {
            var gdiColor = color.ToGDIColor();
            return Color.FromPixel(new Rgba32(gdiColor.R, gdiColor.G, gdiColor.B, gdiColor.A));
        }

        private static Color ToImageSharpColor(SEColor color, float alphaScale)
        {
            var gdiColor = color.ToGDIColor();
            var alpha = (byte)Math.Clamp((int)Math.Round(gdiColor.A * alphaScale), 0, 255);
            return Color.FromPixel(new Rgba32(gdiColor.R, gdiColor.G, gdiColor.B, alpha));
        }

        private static FontRectangle MeasureTextBounds(string text, Font font)
        {
            var textMetrics = TextMeasurer.Measure(text, new TextOptions(font));
            return textMetrics.RenderableBounds;
        }

        private static IPathCollection BuildTextPath(string text, Font font, PointF origin)
        {
            var textOptions = new TextOptions(font)
            {
                Origin = origin
            };

            return TextBuilder.GeneratePaths(text, textOptions);
        }

        private static void FillText(IImageProcessingContext context, string text, Font font, Color color, PointF location)
        {
            var textBounds = MeasureTextBounds(text, font);
            var textPath = BuildTextPath(text, font, new PointF(location.X - textBounds.Left, location.Y - textBounds.Top));

            context.Paint(canvas =>
            {
                canvas.Fill(Brushes.Solid(color), textPath);
            });
        }

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
            var textBounds = MeasureTextBounds(text, font);
            return new Vector2D(textBounds.Width, textBounds.Height);
        }
        public SEImageFile RenderText(string text,Font font,SEColor fillColor)
        {
            var textBounds = MeasureTextBounds(text, font);

            // 创建图像（考虑描边宽度）
            var image = new Image<Rgba32>(
                Math.Max(1, (int)Math.Ceiling(textBounds.Width)),
                Math.Max(1, (int)Math.Ceiling(textBounds.Height)));
            
            image.Mutate(ctx =>
            {
                var textPath = BuildTextPath(text, font, new PointF(-textBounds.Left, -textBounds.Top));
                ctx.Paint(canvas =>
                {
                    canvas.Fill(Brushes.Solid(ToImageSharpColor(fillColor)), textPath);
                });
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
            var textBounds = MeasureTextBounds(text, font);

            // 创建图像（考虑描边宽度）
            var image = new Image<Rgba32>(
                Math.Max(1, (int)Math.Ceiling(textBounds.Width + outlineWidth * 2)),
                Math.Max(1, (int)Math.Ceiling(textBounds.Height + outlineWidth * 2)));

            image.Mutate<Rgba32>(ctx =>
            {
                var baseOrigin = new PointF(outlineWidth - textBounds.Left, outlineWidth - textBounds.Top);
                var fillBrush = Brushes.Solid(ToImageSharpColor(fillColor));
                var outlineBrush = Brushes.Solid(ToImageSharpColor(outlineColor));
                var outlineStep = outlineWidth <= 0 ? 1f : Math.Max(outlineWidth / 2f, 0.5f);

                // 先绘制描边（多次偏移绘制来模拟描边）
                for (float x = -outlineWidth; x <= outlineWidth; x += outlineStep)
                {
                    for (float y = -outlineWidth; y <= outlineWidth; y += outlineStep)
                    {
                        if (Math.Sqrt(x * x + y * y) <= outlineWidth)
                        {
                            var outlinePath = BuildTextPath(text, font, new PointF(baseOrigin.X + x, baseOrigin.Y + y));
                            ctx.Paint(canvas =>
                            {
                                canvas.Fill(outlineBrush, outlinePath);
                            });
                        }
                    }
                }

                // 再绘制填充文本
                var fillPath = BuildTextPath(text, font, baseOrigin);
                ctx.Paint(canvas =>
                {
                    canvas.Fill(fillBrush, fillPath);
                });
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
            var textBounds = MeasureTextBounds(text, font);

            // 计算图像尺寸（考虑阴影偏移）
            var padding = Math.Max(Math.Abs(shadowOffset.X), Math.Abs(shadowOffset.Y)) + shadowBlur;
            var image = new Image<Rgba32>(
                Math.Max(1, (int)Math.Ceiling(textBounds.Width + padding * 2)),
                Math.Max(1, (int)Math.Ceiling(textBounds.Height + padding * 2)));

            image.Mutate<Rgba32>(ctx =>
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

                        FillText(ctx, text, font, ToImageSharpColor(shadowColor, 0.7f - i * 0.2f), new PointF(padding + offset.X, padding + offset.Y));
                    }
                }
                else
                {
                    // 简单阴影
                    FillText(ctx, text, font, ToImageSharpColor(shadowColor), new PointF(padding + shadowOffset.X, padding + shadowOffset.Y));
                }

                // 绘制主文本
                FillText(ctx, text, font, ToImageSharpColor(textColor), new PointF(padding, padding));
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
                var size = MeasureTextBounds(part.Text, part.Font);

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
                    var size = MeasureTextBounds(part.Text, part.Font);

                    // 检查是否需要换行
                    if (x + size.Width > maxWidth && x > 0)
                    {
                        x = 0;
                        y += lineHeight;
                        lineHeight = 0;
                    }

                    // 绘制文本部分
                    FillText(ctx, part.Text, part.Font, part.Color, new PointF(x, y));

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
                FillText(ctx, text, font, ToImageSharpColor(fillColor), new PointF(0, 0));
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
                var fillColorValue = ToImageSharpColor(fillColor);
                var outlineColorValue = ToImageSharpColor(outlineColor);
                var outlineStep = outlineWidth <= 0 ? 1f : Math.Max(outlineWidth / 2f, 0.5f);

                // 先绘制描边（多次偏移绘制来模拟描边）
                for (float x = -outlineWidth; x <= outlineWidth; x += outlineStep)
                {
                    for (float y = -outlineWidth; y <= outlineWidth; y += outlineStep)
                    {
                        if (Math.Sqrt(x * x + y * y) <= outlineWidth)
                        {
                            FillText(ctx, text, font, outlineColorValue, new PointF(outlineWidth + x, outlineWidth + y));
                        }
                    }
                }

                // 再绘制填充文本
                FillText(ctx, text, font, fillColorValue, new PointF(outlineWidth, outlineWidth));
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
            var textBounds = MeasureTextBounds(text, font);

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

                        FillText(ctx, text, font, ToImageSharpColor(shadowColor, 0.7f - i * 0.2f), new PointF(padding + offset.X, padding + offset.Y));
                    }
                }
                else
                {
                    // 简单阴影
                    FillText(ctx, text, font, ToImageSharpColor(shadowColor), new PointF(padding + shadowOffset.X, padding + shadowOffset.Y));
                }

                // 绘制主文本
                FillText(ctx, text, font, ToImageSharpColor(textColor), new PointF(padding, padding));
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
                var size = MeasureTextBounds(part.Text, part.Font);

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
                    var size = MeasureTextBounds(part.Text, part.Font);

                    // 检查是否需要换行
                    if (x + size.Width > maxWidth && x > 0)
                    {
                        x = 0;
                        y += lineHeight;
                        lineHeight = 0;
                    }

                    // 绘制文本部分
                    FillText(ctx, part.Text, part.Font, part.Color, new PointF(x, y));

                    x += size.Width;
                    lineHeight = Math.Max(lineHeight, size.Height);
                }
            });
        }
    }
}
