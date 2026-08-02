using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using StbTrueTypeSharp;

namespace SaturnEngine.SEUI.Render
{
    /// <summary>
    /// 字形信息：UV 坐标、尺寸、相对绘制原点的偏移、前进宽度。
    /// </summary>
    public struct SEGlyph
    {
        public UVRect UV;
        public float Width;
        public float Height;
        /// <summary>相对文本绘制起点的水平偏移（像素）。</summary>
        public float OffsetX;
        /// <summary>相对文本行顶部的垂直偏移（像素，左上原点坐标系）。</summary>
        public float OffsetY;
        public float Advance;
    }

    /// <summary>
    /// UI 字体：用 StbTrueTypeSharp 加载 TTF，按像素高度按需烘烤码点为 8 位灰度，
    /// 转 RGBA 写入 <see cref="SEUIAtlas"/>；提供字形信息、文本测量与行高；
    /// 缓存已烘烤字形，缺字时回退到 '?'。
    /// </summary>
    public unsafe class SEUIFont : IDisposable
    {
        private readonly StbTrueType.stbtt_fontinfo _info;
        private GCHandle _dataHandle;
        private readonly float _pixelHeight;
        private readonly float _scale;
        private readonly SEUIAtlas _atlas;
        private readonly Dictionary<int, SEGlyph> _glyphCache = new();
        private bool _disposed;

        public float Ascent { get; }
        public float Descent { get; }
        public float LineGap { get; }
        public float LineHeight { get; }
        public float PixelHeight => _pixelHeight;

        public SEUIFont(byte[] ttfData, float pixelHeight, SEUIAtlas atlas)
        {
            ArgumentNullException.ThrowIfNull(ttfData);
            ArgumentNullException.ThrowIfNull(atlas);

            _pixelHeight = pixelHeight;
            _atlas = atlas;

            // stbtt_fontinfo 内部保存原始数据指针，必须固定住托管数组
            _dataHandle = GCHandle.Alloc(ttfData, GCHandleType.Pinned);
            byte* dataPtr = (byte*)_dataHandle.AddrOfPinnedObject();

            _info = new StbTrueType.stbtt_fontinfo();
            int offset = StbTrueType.stbtt_GetFontOffsetForIndex(dataPtr, 0);
            if (StbTrueType.stbtt_InitFont(_info, dataPtr, offset) == 0)
            {
                _dataHandle.Free();
                throw new InvalidOperationException("Failed to initialize TTF font.");
            }

            _scale = StbTrueType.stbtt_ScaleForPixelHeight(_info, pixelHeight);

            int ascent, descent, lineGap;
            StbTrueType.stbtt_GetFontVMetrics(_info, &ascent, &descent, &lineGap);
            Ascent = ascent * _scale;
            Descent = descent * _scale;
            LineGap = lineGap * _scale;
            LineHeight = Ascent - Descent + LineGap;
        }

        /// <summary>
        /// 获取指定码点的字形，未缓存时烘烤并注册到图集。
        /// </summary>
        public SEGlyph GetGlyph(int codepoint)
        {
            if (_glyphCache.TryGetValue(codepoint, out var cached))
                return cached;

            int glyphIndex = StbTrueType.stbtt_FindGlyphIndex(_info, codepoint);
            if (glyphIndex == 0)
                glyphIndex = StbTrueType.stbtt_FindGlyphIndex(_info, '?');

            int advanceWidth, leftSideBearing;
            StbTrueType.stbtt_GetGlyphHMetrics(_info, glyphIndex, &advanceWidth, &leftSideBearing);

            int x0, y0, x1, y1;
            StbTrueType.stbtt_GetGlyphBitmapBox(_info, glyphIndex, _scale, _scale, &x0, &y0, &x1, &y1);

            int width = x1 - x0;
            int height = y1 - y0;

            UVRect uv;
            if (width > 0 && height > 0)
            {
                byte[] bitmap = new byte[width * height];
                fixed (byte* ptr = bitmap)
                {
                    StbTrueType.stbtt_MakeGlyphBitmap(_info, ptr, width, height, width, _scale, _scale, glyphIndex);
                }

                uv = _atlas.RegisterGlyph(bitmap, width, height, $"glyph_{_pixelHeight}_{codepoint}");
            }
            else
            {
                // 空字形（如空格）复用图集白像素
                uv = _atlas.WhitePixelUV;
            }

            var glyph = new SEGlyph
            {
                UV = uv,
                Width = width,
                Height = height,
                OffsetX = x0,
                // y0 是相对基线向上为负，加上 Ascent 转成相对行顶部的下移量
                OffsetY = y0 + Ascent,
                Advance = advanceWidth * _scale
            };

            _glyphCache[codepoint] = glyph;
            return glyph;
        }

        /// <summary>
        /// 获取相邻码点的字距调整量（像素）。
        /// </summary>
        public float GetKerning(int previous, int current)
        {
            if (previous == 0)
                return 0f;
            return StbTrueType.stbtt_GetCodepointKernAdvance(_info, previous, current) * _scale;
        }

        /// <summary>
        /// 测量单行文本的宽高（像素）。
        /// </summary>
        public (float width, float height) Measure(string text)
        {
            if (string.IsNullOrEmpty(text))
                return (0f, LineHeight);

            float x = 0f;
            int previous = 0;

            foreach (char c in text)
            {
                x += GetKerning(previous, c);
                x += GetGlyph(c).Advance;
                previous = c;
            }

            return (x, LineHeight);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_dataHandle.IsAllocated)
                _dataHandle.Free();

            _glyphCache.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~SEUIFont() => Dispose();
    }
}
