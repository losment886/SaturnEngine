using SaturnEngine.Asset;
using SaturnEngine.SEGraphics.Native;
using System;
using System.Collections.Generic;

namespace SaturnEngine.SEUI.Render
{
    /// <summary>
    /// 像素空间的轴对齐矩形（左上原点）。
    /// </summary>
    public struct SEUIRect
    {
        public float X, Y, W, H;

        public SEUIRect(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public float Right => X + W;
        public float Bottom => Y + H;

        /// <summary>求两个矩形的交集，无交集时返回零面积矩形。</summary>
        public SEUIRect Intersect(SEUIRect other)
        {
            float x0 = Math.Max(X, other.X);
            float y0 = Math.Max(Y, other.Y);
            float x1 = Math.Min(Right, other.Right);
            float y1 = Math.Min(Bottom, other.Bottom);
            if (x1 <= x0 || y1 <= y0)
                return new SEUIRect(x0, y0, 0f, 0f);
            return new SEUIRect(x0, y0, x1 - x0, y1 - y0);
        }
    }

    /// <summary>
    /// UI 绘制列表：提供 AddRectFilled / AddImage / AddText / PushClipRect / PopClipRect；
    /// 输出 <see cref="NRVertex"/>[] + uint[]，坐标系为窗口像素（左上原点）；
    /// 顶点 Color 写入 Tint × 累积 Opacity；纯色使用图集白像素 UV；支持按角度做四顶点旋转。
    /// </summary>
    public class SEUIDrawList
    {
        private readonly List<NRVertex> _vertices = new();
        private readonly List<uint> _indices = new();
        private readonly Stack<SEUIRect> _clipStack = new();
        private readonly SEUIAtlas _atlas;

        public SEUIDrawList(SEUIAtlas atlas)
        {
            _atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
        }

        public int VertexCount => _vertices.Count;
        public int IndexCount => _indices.Count;

        public void Clear()
        {
            _vertices.Clear();
            _indices.Clear();
            _clipStack.Clear();
        }

        public (NRVertex[] vertices, uint[] indices) GetData()
            => (_vertices.ToArray(), _indices.ToArray());

        /// <summary>压入裁剪矩形，与当前裁剪区求交。</summary>
        public void PushClipRect(SEUIRect clip)
        {
            if (_clipStack.Count > 0)
                clip = clip.Intersect(_clipStack.Peek());
            _clipStack.Push(clip);
        }

        public void PopClipRect()
        {
            if (_clipStack.Count > 0)
                _clipStack.Pop();
        }

        /// <summary>纯色填充矩形。</summary>
        public void AddRectFilled(SEUIRect rect, SEColor color, double opacity, double angle = 0)
            => AddQuad(rect, _atlas.WhitePixelUV, color, opacity, angle);

        /// <summary>图集贴图矩形。</summary>
        public void AddImage(SEUIRect rect, UVRect uv, SEColor tint, double opacity, double angle = 0)
            => AddQuad(rect, uv, tint, opacity, angle);

        /// <summary>
        /// 单行文本，position 为文本行左上角。
        /// </summary>
        public void AddText(string text, float x, float y, SEColor color, double opacity, SEUIFont font)
        {
            if (string.IsNullOrEmpty(text) || font is null)
                return;

            float penX = x;
            int previous = 0;

            foreach (char c in text)
            {
                penX += font.GetKerning(previous, c);
                var glyph = font.GetGlyph(c);

                if (glyph.Width > 0f && glyph.Height > 0f)
                {
                    var rect = new SEUIRect(
                        penX + glyph.OffsetX,
                        y + glyph.OffsetY,
                        glyph.Width,
                        glyph.Height);
                    AddQuad(rect, glyph.UV, color, opacity, 0);
                }

                penX += glyph.Advance;
                previous = c;
            }
        }

        private void AddQuad(SEUIRect rect, UVRect uv, SEColor tint, double opacity, double angle)
        {
            if (rect.W <= 0f || rect.H <= 0f || opacity <= 0d)
                return;

            bool rotated = Math.Abs(angle) > 1e-6;

            // 无旋转时可直接按裁剪矩形做几何裁剪（含 UV 重映射）
            if (!rotated && _clipStack.Count > 0)
            {
                var clip = _clipStack.Peek();
                var clipped = rect.Intersect(clip);
                if (clipped.W <= 0f || clipped.H <= 0f)
                    return;

                if (clipped.X != rect.X || clipped.Y != rect.Y || clipped.W != rect.W || clipped.H != rect.H)
                {
                    float du = uv.UMax - uv.UMin;
                    float dv = uv.VMax - uv.VMin;
                    float u0 = uv.UMin + du * (clipped.X - rect.X) / rect.W;
                    float u1 = uv.UMin + du * (clipped.Right - rect.X) / rect.W;
                    float v0 = uv.VMin + dv * (clipped.Y - rect.Y) / rect.H;
                    float v1 = uv.VMin + dv * (clipped.Bottom - rect.Y) / rect.H;
                    uv = new UVRect(u0, v0, u1, v1);
                    rect = clipped;
                }
            }

            var color = new NRFloat4
            {
                X = (float)tint.R,
                Y = (float)tint.G,
                Z = (float)tint.B,
                W = (float)(tint.A * opacity)
            };

            Span<float> px = stackalloc float[4];
            Span<float> py = stackalloc float[4];

            px[0] = rect.X; py[0] = rect.Y;              // 左上
            px[1] = rect.Right; py[1] = rect.Y;          // 右上
            px[2] = rect.Right; py[2] = rect.Bottom;     // 右下
            px[3] = rect.X; py[3] = rect.Bottom;         // 左下

            if (rotated)
            {
                float cx = rect.X + rect.W * 0.5f;
                float cy = rect.Y + rect.H * 0.5f;
                float cos = (float)Math.Cos(angle);
                float sin = (float)Math.Sin(angle);

                for (int i = 0; i < 4; i++)
                {
                    float dx = px[i] - cx;
                    float dy = py[i] - cy;
                    px[i] = cx + dx * cos - dy * sin;
                    py[i] = cy + dx * sin + dy * cos;
                }
            }

            Span<float> u = stackalloc float[4] { uv.UMin, uv.UMax, uv.UMax, uv.UMin };
            Span<float> v = stackalloc float[4] { uv.VMin, uv.VMin, uv.VMax, uv.VMax };

            uint baseIndex = (uint)_vertices.Count;

            for (int i = 0; i < 4; i++)
            {
                _vertices.Add(new NRVertex
                {
                    Position = new NRFloat3 { X = px[i], Y = py[i], Z = 0f },
                    Normal = new NRFloat3 { X = 0f, Y = 0f, Z = 1f },
                    Tangent = new NRFloat4 { X = 1f, Y = 0f, Z = 0f, W = 1f },
                    UV0 = new NRFloat2 { X = u[i], Y = v[i] },
                    UV1 = new NRFloat2 { X = u[i], Y = v[i] },
                    Color = color,
                    Weights = default
                });
            }

            _indices.Add(baseIndex);
            _indices.Add(baseIndex + 1);
            _indices.Add(baseIndex + 2);
            _indices.Add(baseIndex);
            _indices.Add(baseIndex + 2);
            _indices.Add(baseIndex + 3);
        }
    }
}
