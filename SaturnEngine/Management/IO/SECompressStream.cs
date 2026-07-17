using System.IO;
using System.Runtime.CompilerServices;

namespace SaturnEngine.Management.IO
{
    /// <summary>
    /// 提供 LZ4 压缩与解压支持。
    /// </summary>
    public static class SECompressStream
    {
        private static readonly byte[] ChunkedMagic = [(byte)'S', (byte)'E', (byte)'C', (byte)'2'];
        public const byte ChunkedFormatVersion = 1;
        public const int DefaultChunkSize = 4 * 1024 * 1024;
        private const byte ChunkRaw = 0;
        private const byte ChunkCompressed = 1;
        private const int HASH_SIZE = 1 << 16;
        private const int MIN_MATCH = 4;
        private const int LAST_LITERALS = 5;
        private const int MAX_OFFSET = 65535;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Hash4(byte[] buf, int pos)
        {
            uint value = (uint)(buf[pos] | (buf[pos + 1] << 8) | (buf[pos + 2] << 16) | (buf[pos + 3] << 24));
            return (int)((value * 2654435761u) >> 16);
        }

        public static byte[] Compress(byte[] src)
        {
            ArgumentNullException.ThrowIfNull(src);
            int srcLength = src.Length;
            if (srcLength == 0)
            {
                return [];
            }
            if (srcLength < MIN_MATCH)
            {
                byte[] raw = new byte[1 + srcLength + ((srcLength >= 15) ? ((srcLength - 15) / 255 + 1) : 0)];
                int rawIndex = 0;
                raw[rawIndex++] = (byte)(Math.Min(srcLength, 15) << 4);
                int extraLiteral = srcLength - 15;
                while (extraLiteral >= 255)
                {
                    raw[rawIndex++] = 255;
                    extraLiteral -= 255;
                }
                if (srcLength >= 15)
                {
                    raw[rawIndex++] = (byte)Math.Max(extraLiteral, 0);
                }
                Buffer.BlockCopy(src, 0, raw, rawIndex, srcLength);
                return raw;
            }

            int maxCompressedLength = srcLength + (srcLength / 255) + 16;
            byte[] dst = new byte[maxCompressedLength];
            int[] hashTable = new int[HASH_SIZE];
            Array.Fill(hashTable, -1);

            int srcIndex = 0;
            int anchor = 0;
            int dstIndex = 0;
            int matchLimit = srcLength - LAST_LITERALS;

            while (srcIndex <= matchLimit - MIN_MATCH)
            {
                int hash = Hash4(src, srcIndex);
                int matchIndex = hashTable[hash];
                hashTable[hash] = srcIndex;

                if (matchIndex < 0 || srcIndex - matchIndex > MAX_OFFSET ||
                    src[matchIndex] != src[srcIndex] ||
                    src[matchIndex + 1] != src[srcIndex + 1] ||
                    src[matchIndex + 2] != src[srcIndex + 2] ||
                    src[matchIndex + 3] != src[srcIndex + 3])
                {
                    srcIndex++;
                    continue;
                }

                int literalLength = srcIndex - anchor;
                int tokenIndex = dstIndex++;
                byte token = 0;

                if (literalLength >= 15)
                {
                    token |= 0xF0;
                    int extraLiteral = literalLength - 15;
                    while (extraLiteral >= 255)
                    {
                        dst[dstIndex++] = 255;
                        extraLiteral -= 255;
                    }
                    dst[dstIndex++] = (byte)extraLiteral;
                }
                else
                {
                    token |= (byte)(literalLength << 4);
                }

                if (literalLength > 0)
                {
                    Buffer.BlockCopy(src, anchor, dst, dstIndex, literalLength);
                    dstIndex += literalLength;
                }

                int offset = srcIndex - matchIndex;
                dst[dstIndex++] = (byte)offset;
                dst[dstIndex++] = (byte)(offset >> 8);

                int matchLength = MIN_MATCH;
                int matchPtr = matchIndex + MIN_MATCH;
                int srcPtr = srcIndex + MIN_MATCH;
                while (srcPtr < srcLength && src[matchPtr] == src[srcPtr])
                {
                    matchPtr++;
                    srcPtr++;
                    matchLength++;
                }

                int encodedMatchLength = matchLength - MIN_MATCH;
                if (encodedMatchLength >= 15)
                {
                    token |= 0x0F;
                    int extraMatch = encodedMatchLength - 15;
                    while (extraMatch >= 255)
                    {
                        dst[dstIndex++] = 255;
                        extraMatch -= 255;
                    }
                    dst[dstIndex++] = (byte)extraMatch;
                }
                else
                {
                    token |= (byte)encodedMatchLength;
                }

                dst[tokenIndex] = token;

                srcIndex += matchLength;
                anchor = srcIndex;

                if (srcIndex <= srcLength - MIN_MATCH)
                {
                    hashTable[Hash4(src, srcIndex - 2)] = srcIndex - 2;
                    hashTable[Hash4(src, srcIndex - 1)] = srcIndex - 1;
                }
            }

            int lastLiteralLength = srcLength - anchor;
            int lastTokenIndex = dstIndex++;
            byte lastToken = 0;
            if (lastLiteralLength >= 15)
            {
                lastToken |= 0xF0;
                int extraLiteral = lastLiteralLength - 15;
                while (extraLiteral >= 255)
                {
                    dst[dstIndex++] = 255;
                    extraLiteral -= 255;
                }
                dst[dstIndex++] = (byte)extraLiteral;
            }
            else
            {
                lastToken |= (byte)(lastLiteralLength << 4);
            }

            dst[lastTokenIndex] = lastToken;
            if (lastLiteralLength > 0)
            {
                Buffer.BlockCopy(src, anchor, dst, dstIndex, lastLiteralLength);
                dstIndex += lastLiteralLength;
            }

            byte[] result = new byte[dstIndex];
            Buffer.BlockCopy(dst, 0, result, 0, dstIndex);
            return result;
        }

        public static byte[] Decompress(byte[] src, int originalSize = -1, int offset = 0)
        {
            ArgumentNullException.ThrowIfNull(src);
            if (offset < 0 || offset > src.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            int srcIndex = offset;
            int srcLength = src.Length;
            int initialSize = originalSize >= 0 ? originalSize : Math.Max((srcLength - offset) * 4, 64);
            byte[] dst = new byte[initialSize];
            int dstIndex = 0;

            while (srcIndex < srcLength)
            {
                byte token = src[srcIndex++];
                int literalLength = token >> 4;

                if (literalLength == 15)
                {
                    byte len;
                    do
                    {
                        if (srcIndex >= srcLength)
                        {
                            throw new InvalidDataException("LZ4 literal length overflow");
                        }
                        len = src[srcIndex++];
                        literalLength += len;
                    }
                    while (len == 255);
                }

                EnsureCapacity(ref dst, dstIndex + literalLength, originalSize);
                if (srcIndex + literalLength > srcLength)
                {
                    throw new InvalidDataException("LZ4 literal exceeds source length");
                }
                Buffer.BlockCopy(src, srcIndex, dst, dstIndex, literalLength);
                srcIndex += literalLength;
                dstIndex += literalLength;

                if (srcIndex >= srcLength)
                {
                    break;
                }

                if (srcIndex + 1 >= srcLength)
                {
                    throw new InvalidDataException("LZ4 missing match offset");
                }

                int matchOffset = src[srcIndex] | (src[srcIndex + 1] << 8);
                srcIndex += 2;
                if (matchOffset <= 0 || matchOffset > dstIndex)
                {
                    throw new InvalidDataException("LZ4 invalid match offset");
                }

                int matchLength = (token & 0x0F) + MIN_MATCH;
                if ((token & 0x0F) == 15)
                {
                    byte len;
                    do
                    {
                        if (srcIndex >= srcLength)
                        {
                            throw new InvalidDataException("LZ4 match length overflow");
                        }
                        len = src[srcIndex++];
                        matchLength += len;
                    }
                    while (len == 255);
                }

                EnsureCapacity(ref dst, dstIndex + matchLength, originalSize);
                int matchIndex = dstIndex - matchOffset;
                for (int i = 0; i < matchLength; i++)
                {
                    dst[dstIndex++] = dst[matchIndex + i];
                }
            }

            if (originalSize >= 0 && dstIndex != originalSize)
            {
                throw new InvalidDataException("LZ4 decompressed size mismatch");
            }

            if (dstIndex == dst.Length)
            {
                return dst;
            }

            byte[] result = new byte[dstIndex];
            Buffer.BlockCopy(dst, 0, result, 0, dstIndex);
            return result;
        }

        public static byte[] CompressWithSize(byte[] src)
        {
            ArgumentNullException.ThrowIfNull(src);
            byte[] compressed = Compress(src);
            byte[] result = new byte[compressed.Length + 4];
            int length = src.Length;
            result[0] = (byte)length;
            result[1] = (byte)(length >> 8);
            result[2] = (byte)(length >> 16);
            result[3] = (byte)(length >> 24);
            Buffer.BlockCopy(compressed, 0, result, 4, compressed.Length);
            return result;
        }

        public static byte[] DecompressWithSize(byte[] src)
        {
            ArgumentNullException.ThrowIfNull(src);
            if (src.Length < 4)
            {
                throw new InvalidDataException("LZ4 source missing size header");
            }

            int originalSize = src[0] | (src[1] << 8) | (src[2] << 16) | (src[3] << 24);
            if (originalSize < 0)
            {
                throw new InvalidDataException("LZ4 invalid original size");
            }

            return Decompress(src, originalSize, 4);
        }

        public static long CompressToChunkedStream(Stream source, Stream destination, int chunkSize = DefaultChunkSize, bool leaveOpen = true)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);
            if (!source.CanRead)
            {
                throw new ArgumentException("Source stream must be readable", nameof(source));
            }
            if (!destination.CanWrite)
            {
                throw new ArgumentException("Destination stream must be writable", nameof(destination));
            }
            if (chunkSize < MIN_MATCH)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize));
            }

            long startPosition = destination.CanSeek ? destination.Position : 0;
            long originalPosition = source.CanSeek ? source.Position : 0;
            long totalSourceLength = source.CanSeek ? source.Length - source.Position : -1;

            destination.Write(ChunkedMagic, 0, ChunkedMagic.Length);
            destination.WriteByte(ChunkedFormatVersion);
            WriteInt32(destination, chunkSize);
            WriteInt64(destination, totalSourceLength);

            byte[] chunkBuffer = new byte[chunkSize];
            while (true)
            {
                int bytesRead = ReadAtMost(source, chunkBuffer, 0, chunkBuffer.Length);
                if (bytesRead <= 0)
                {
                    break;
                }

                byte[] rawChunk = chunkBuffer;
                if (bytesRead != chunkBuffer.Length)
                {
                    rawChunk = new byte[bytesRead];
                    Buffer.BlockCopy(chunkBuffer, 0, rawChunk, 0, bytesRead);
                }

                byte[] compressedChunk = Compress(rawChunk);
                bool useCompressed = compressedChunk.Length < bytesRead;
                byte chunkType = useCompressed ? ChunkCompressed : ChunkRaw;
                byte[] storedChunk = useCompressed ? compressedChunk : rawChunk;

                destination.WriteByte(chunkType);
                WriteInt32(destination, bytesRead);
                WriteInt32(destination, storedChunk.Length);
                destination.Write(storedChunk, 0, storedChunk.Length);
            }

            if (!leaveOpen)
            {
                source.Dispose();
                destination.Dispose();
            }

            if (!destination.CanSeek)
            {
                return -1;
            }

            return destination.Position - startPosition;
        }

        public static long DecompressChunkedStream(Stream source, Stream destination, long storedLength = -1, bool leaveOpen = true)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);
            if (!source.CanRead)
            {
                throw new ArgumentException("Source stream must be readable", nameof(source));
            }
            if (!destination.CanWrite)
            {
                throw new ArgumentException("Destination stream must be writable", nameof(destination));
            }

            long startPosition = source.CanSeek ? source.Position : 0;
            ValidateChunkedHeader(source, out int chunkSize, out long declaredOriginalLength);

            if (chunkSize < MIN_MATCH)
            {
                throw new InvalidDataException("Invalid chunk size");
            }

            byte[] reusableRawBuffer = new byte[chunkSize];
            long totalWritten = 0;
            while (storedLength < 0 || (source.CanSeek && source.Position - startPosition < storedLength))
            {
                int chunkTypeValue = source.ReadByte();
                if (chunkTypeValue < 0)
                {
                    break;
                }

                int rawLength = ReadInt32(source);
                int storedChunkLength = ReadInt32(source);
                if (rawLength < 0 || storedChunkLength < 0)
                {
                    throw new InvalidDataException("Invalid chunk length");
                }

                byte[] storedChunk = new byte[storedChunkLength];
                source.ReadExactly(storedChunk, 0, storedChunkLength);

                if (chunkTypeValue == ChunkRaw)
                {
                    if (storedChunkLength != rawLength)
                    {
                        throw new InvalidDataException("Chunk raw length mismatch");
                    }
                    destination.Write(storedChunk, 0, storedChunkLength);
                    totalWritten += storedChunkLength;
                }
                else if (chunkTypeValue == ChunkCompressed)
                {
                    byte[] rawChunk = Decompress(storedChunk, rawLength);
                    destination.Write(rawChunk, 0, rawChunk.Length);
                    totalWritten += rawChunk.Length;
                }
                else
                {
                    throw new InvalidDataException("Unknown chunk type");
                }

                if (declaredOriginalLength >= 0 && totalWritten >= declaredOriginalLength)
                {
                    break;
                }

                if (!source.CanSeek && storedLength >= 0 && totalWritten >= declaredOriginalLength)
                {
                    break;
                }
            }

            if (declaredOriginalLength >= 0 && totalWritten != declaredOriginalLength)
            {
                throw new InvalidDataException("Chunked decompressed size mismatch");
            }

            if (!leaveOpen)
            {
                source.Dispose();
                destination.Dispose();
            }

            return totalWritten;
        }

        public static bool IsChunkedStream(Stream source)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (!source.CanRead || !source.CanSeek)
            {
                return false;
            }

            long position = source.Position;
            try
            {
                if (source.Length - source.Position < ChunkedMagic.Length + 1)
                {
                    return false;
                }

                byte[] header = new byte[ChunkedMagic.Length];
                source.ReadExactly(header, 0, header.Length);
                int version = source.ReadByte();
                return version == ChunkedFormatVersion && header.SequenceEqual(ChunkedMagic);
            }
            finally
            {
                source.Seek(position, SeekOrigin.Begin);
            }
        }

        public static long ReadChunkedOriginalLength(Stream source)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (!source.CanRead || !source.CanSeek)
            {
                throw new NotSupportedException("Stream must support read and seek");
            }

            long position = source.Position;
            try
            {
                ValidateChunkedHeader(source, out _, out long originalLength);
                return originalLength;
            }
            finally
            {
                source.Seek(position, SeekOrigin.Begin);
            }
        }

        private static void ValidateChunkedHeader(Stream source, out int chunkSize, out long originalLength)
        {
            byte[] header = new byte[ChunkedMagic.Length];
            source.ReadExactly(header, 0, header.Length);
            if (!header.SequenceEqual(ChunkedMagic))
            {
                throw new InvalidDataException("Invalid chunked compression header");
            }

            int version = source.ReadByte();
            if (version != ChunkedFormatVersion)
            {
                throw new InvalidDataException("Unsupported chunked compression version");
            }

            chunkSize = ReadInt32(source);
            originalLength = ReadInt64(source);
        }

        private static int ReadAtMost(Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (bytesRead <= 0)
                {
                    break;
                }
                totalRead += bytesRead;
            }
            return totalRead;
        }

        private static void WriteInt32(Stream stream, int value)
        {
            byte[] buffer = new byte[4];
            buffer[0] = (byte)value;
            buffer[1] = (byte)(value >> 8);
            buffer[2] = (byte)(value >> 16);
            buffer[3] = (byte)(value >> 24);
            stream.Write(buffer, 0, buffer.Length);
        }

        private static void WriteInt64(Stream stream, long value)
        {
            byte[] buffer = new byte[8];
            buffer[0] = (byte)value;
            buffer[1] = (byte)(value >> 8);
            buffer[2] = (byte)(value >> 16);
            buffer[3] = (byte)(value >> 24);
            buffer[4] = (byte)(value >> 32);
            buffer[5] = (byte)(value >> 40);
            buffer[6] = (byte)(value >> 48);
            buffer[7] = (byte)(value >> 56);
            stream.Write(buffer, 0, buffer.Length);
        }

        private static int ReadInt32(Stream stream)
        {
            byte[] buffer = new byte[4];
            stream.ReadExactly(buffer, 0, buffer.Length);
            return buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24);
        }

        private static long ReadInt64(Stream stream)
        {
            byte[] buffer = new byte[8];
            stream.ReadExactly(buffer, 0, buffer.Length);
            return buffer[0]
                | ((long)buffer[1] << 8)
                | ((long)buffer[2] << 16)
                | ((long)buffer[3] << 24)
                | ((long)buffer[4] << 32)
                | ((long)buffer[5] << 40)
                | ((long)buffer[6] << 48)
                | ((long)buffer[7] << 56);
        }

        private static void EnsureCapacity(ref byte[] buffer, int requiredLength, int fixedSize)
        {
            if (requiredLength <= buffer.Length)
            {
                return;
            }

            if (fixedSize >= 0)
            {
                throw new InvalidDataException("LZ4 output exceeds declared size");
            }

            int newLength = buffer.Length == 0 ? 64 : buffer.Length;
            while (newLength < requiredLength)
            {
                newLength <<= 1;
            }
            Array.Resize(ref buffer, newLength);
        }
    }
}
