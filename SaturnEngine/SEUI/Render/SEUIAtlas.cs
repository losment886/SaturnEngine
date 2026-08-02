using SaturnEngine.Asset;
using SaturnEngine.SEGraphics.Native;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;

namespace SaturnEngine.SEUI.Render
{
    /// <summary>
    /// 图集内的归一化 UV 矩形。
    /// </summary>
    public struct UVRect
    {
        public float UMin, VMin, UMax, VMax;

        public UVRect(float uMin, float vMin, float uMax, float vMax)
        {
            UMin = uMin;
            VMin = vMin;
            UMax = uMax;
            VMax = vMax;
        }
    }

    /// <summary>
    /// 动态 RGBA8 图集，货架式(shelf)装箱；(0,0) 处保留 1×1 白像素供纯色矩形复用；
    /// 维护脏标记，通过 NRNative.CreateTexture / UpdateTexture 创建与上传；
    /// 容量不足时按 2 倍扩容并重建纹理。
    /// </summary>
    public unsafe class SEUIAtlas : IDisposable
    {
        private int _width;
        private int _height;
        private byte[] _pixels;
        private bool _disposed;

        private ulong _textureHandle;
        private bool _textureCreated;
        private bool _dirty;

        private struct Shelf
        {
            public int Y;
            public int Height;
            public int X;
        }

        private readonly List<Shelf> _shelves = new();
        private readonly Dictionary<string, UVRect> _registered = new();

        public int Width => _width;
        public int Height => _height;
        public ulong TextureHandle => _textureHandle;

        /// <summary>白像素在图集中的 UV（用于纯色绘制）。</summary>
        public UVRect WhitePixelUV => new(0f, 0f, 1f / _width, 1f / _height);

        public SEUIAtlas(int initialSize = 512)
        {
            _width = initialSize;
            _height = initialSize;
            _pixels = new byte[_width * _height * 4];

            // (0,0) 白像素
            SetWhitePixel();

            // 第一个货架从 Y=1 开始，避开白像素行
            _shelves.Add(new Shelf { Y = 1, Height = 0, X = 0 });
            _dirty = true;
        }

        private void SetWhitePixel()
        {
            _pixels[0] = 255;
            _pixels[1] = 255;
            _pixels[2] = 255;
            _pixels[3] = 255;
        }

        /// <summary>
        /// 注册 <see cref="SEImageFile"/> 到图集，返回 UV 矩形；同 key 复用。
        /// </summary>
        public UVRect Register(SEImageFile image, string key)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (_registered.TryGetValue(key, out var existing))
                return existing;

            if (image.BaseImage is null)
                throw new ArgumentException("SEImageFile has no loaded image.", nameof(image));

            using var rgba = image.BaseImage.CloneAs<Rgba32>();
            int imgWidth = rgba.Width;
            int imgHeight = rgba.Height;

            byte[] source = new byte[imgWidth * imgHeight * 4];
            rgba.CopyPixelDataTo(source);

            var slot = AllocateOrExpand(imgWidth, imgHeight);
            CopyPixels(source, imgWidth, imgHeight, slot.x, slot.y);

            var uv = MakeUV(slot.x, slot.y, imgWidth, imgHeight);
            _registered[key] = uv;
            _dirty = true;
            return uv;
        }

        /// <summary>
        /// 注册单通道灰度字形位图到图集（转 RGBA 白色 + alpha），返回 UV 矩形。
        /// </summary>
        public UVRect RegisterGlyph(byte[] glyphBitmap, int glyphWidth, int glyphHeight, string key)
        {
            ArgumentNullException.ThrowIfNull(glyphBitmap);

            if (_registered.TryGetValue(key, out var existing))
                return existing;

            if (glyphBitmap.Length < glyphWidth * glyphHeight)
                throw new ArgumentException("Glyph bitmap is smaller than declared size.", nameof(glyphBitmap));

            var slot = AllocateOrExpand(glyphWidth, glyphHeight);
            CopyGlyphPixels(glyphBitmap, glyphWidth, glyphHeight, slot.x, slot.y);

            var uv = MakeUV(slot.x, slot.y, glyphWidth, glyphHeight);
            _registered[key] = uv;
            _dirty = true;
            return uv;
        }

        private UVRect MakeUV(int x, int y, int w, int h) => new(
            (float)x / _width,
            (float)y / _height,
            (float)(x + w) / _width,
            (float)(y + h) / _height);

        private (int x, int y) AllocateOrExpand(int width, int height)
        {
            var slot = Allocate(width, height);
            while (slot is null)
            {
                int before = _width;
                Expand();
                if (_width == before)
                    throw new InvalidOperationException($"Failed to allocate {width}x{height} in UI atlas.");
                slot = Allocate(width, height);
            }
            return slot.Value;
        }

        /// <summary>
        /// 货架式分配矩形区域，失败返回 null。
        /// </summary>
        private (int x, int y)? Allocate(int width, int height)
        {
            if (width > _width)
                return null;

            for (int i = 0; i < _shelves.Count; i++)
            {
                var shelf = _shelves[i];
                if (shelf.Height >= height && shelf.X + width <= _width)
                {
                    int allocX = shelf.X;
                    shelf.X += width;
                    _shelves[i] = shelf;
                    return (allocX, shelf.Y);
                }
            }

            int newShelfY = 1;
            if (_shelves.Count > 0)
            {
                var last = _shelves[^1];
                newShelfY = last.Y + last.Height;
            }

            if (newShelfY + height > _height)
                return null;

            _shelves.Add(new Shelf { Y = newShelfY, Height = height, X = width });
            return (0, newShelfY);
        }

        /// <summary>
        /// 扩容为 2 倍并保留已有像素，纹理需要重建。
        /// </summary>
        private void Expand()
        {
            int newWidth = _width * 2;
            int newHeight = _height * 2;
            byte[] newPixels = new byte[newWidth * newHeight * 4];

            for (int y = 0; y < _height; y++)
            {
                Array.Copy(_pixels, y * _width * 4, newPixels, y * newWidth * 4, _width * 4);
            }

            // 已注册的 UV 基于旧尺寸，需要按比例重映射
            float scaleX = (float)_width / newWidth;
            float scaleY = (float)_height / newHeight;
            foreach (var key in new List<string>(_registered.Keys))
            {
                var uv = _registered[key];
                _registered[key] = new UVRect(
                    uv.UMin * scaleX,
                    uv.VMin * scaleY,
                    uv.UMax * scaleX,
                    uv.VMax * scaleY);
            }

            _pixels = newPixels;
            _width = newWidth;
            _height = newHeight;

            if (_textureCreated)
            {
                NRNative.DestroyTexture(_textureHandle);
                _textureHandle = 0;
                _textureCreated = false;
            }

            _dirty = true;
        }

        private void CopyPixels(byte[] srcData, int srcWidth, int srcHeight, int dstX, int dstY)
        {
            for (int y = 0; y < srcHeight; y++)
            {
                int srcIdx = y * srcWidth * 4;
                int dstIdx = ((dstY + y) * _width + dstX) * 4;
                Array.Copy(srcData, srcIdx, _pixels, dstIdx, srcWidth * 4);
            }
        }

        private void CopyGlyphPixels(byte[] glyphData, int glyphWidth, int glyphHeight, int dstX, int dstY)
        {
            for (int y = 0; y < glyphHeight; y++)
            {
                for (int x = 0; x < glyphWidth; x++)
                {
                    byte alpha = glyphData[y * glyphWidth + x];
                    int idx = ((dstY + y) * _width + (dstX + x)) * 4;
                    _pixels[idx] = 255;
                    _pixels[idx + 1] = 255;
                    _pixels[idx + 2] = 255;
                    _pixels[idx + 3] = alpha;
                }
            }
        }

        /// <summary>
        /// 首次创建纹理或整图上传（原生 ABI 仅支持整图更新）。
        /// </summary>
        public void Flush()
        {
            if (!_dirty)
                return;

            fixed (byte* ptr = _pixels)
            {
                ulong sizeBytes = (ulong)_pixels.Length;

                if (!_textureCreated)
                {
                    var info = new NRTextureCreateInfo
                    {
                        Width = (uint)_width,
                        Height = (uint)_height,
                        Depth = 1,
                        MipLevels = 1,
                        Format = NRTextureFormat.R8G8B8A8Unorm,
                        Type = NRTextureType.Texture2D,
                        WrapU = NRWrapMode.ClampEdge,
                        WrapV = NRWrapMode.ClampEdge,
                        WrapW = NRWrapMode.ClampEdge,
                        FilterLinear = 1,
                        MaxAnisotropy = 0f,
                        Pixels = ptr,
                        PixelsSize = sizeBytes
                    };

                    ulong handle;
                    NRNative.CreateTexture(&info, &handle);
                    _textureHandle = handle;
                    _textureCreated = true;
                }
                else
                {
                    NRNative.UpdateTexture(_textureHandle, ptr, sizeBytes, 0, 0);
                }
            }

            _dirty = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_textureCreated)
            {
                NRNative.DestroyTexture(_textureHandle);
                _textureHandle = 0;
                _textureCreated = false;
            }

            _registered.Clear();
            _shelves.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
