using SaturnEngine.Asset;

namespace SaturnEngine.Management.SEMemory
{
    public class SEMemoryStream : Stream
    {
        public enum SEMemoryStreamMode
        {
            Fixed,//固定长度，不能扩展
            Expandable//可扩展
        }
        public enum SEBlockSize : long
        {
            /// <summary>
            /// 1KB块大小
            /// </summary>
            Stream = 1024,//1KB
            /// <summary>
            /// 4KB块大小
            /// </summary>
            Buffer = 4096,//4KB
            /// <summary>
            /// 16KB块大小
            /// </summary>
            Cache = 16384,//16KB
            /// <summary>
            /// 64KB块大小
            /// </summary>
            TempPool = 65536,//64KB
            /// <summary>
            /// 128KB块大小
            /// </summary>
            Special = 131072,//128KB
            /// <summary>
            /// 1MB块大小
            /// </summary>
            Minimum = 1048576,//1MB
            /// <summary>
            /// 4MB块大小
            /// </summary>
            VerySmall = 4194304,//4MB
            /// <summary>
            /// 32MB块大小
            /// </summary>
            Small = 33554432,//32MB
            /// <summary>
            /// 128MB块大小
            /// </summary>
            Normal = 134217728,//128MB
            /// <summary>
            /// 256MB块大小
            /// </summary>
            Large = 268435456,//256MB
            /// <summary>
            /// 512MB块大小
            /// </summary>
            VeryLarge = 536870812,//512MB
            /// <summary>
            /// 1GB块大小
            /// </summary>
            Maximal = 1073741824,//1GB
            /// <summary>
            /// 2GB块大小
            /// </summary>
            Ocean =  2147483648,//2GB
            /// <summary>
            /// 仅用于自动设置块大小
            /// </summary>
            Auto = 1145141919810//仅限于有给定长度的流，会自动确认大小，否则为Minimum，大小不可更改，除非重置
        }

        long DefaultBlockCapacity = 1048576;//1MB
        int DefaultBlockCapacity_int32 = 1048576;//1MB
        long lg = 0;
        long psi = 0;
        long ndi = 0;
        long nco = 0;

        long tplg = 0;

        SEMemoryStreamSlim[] mss = new SEMemoryStreamSlim[0];
        SEMemoryStreamMode LRSM;
        bool lcv = false;
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => lg;

        public override long Position { get => psi; set => Seek(value, SeekOrigin.Begin); }

        public long MemoryUsage => tplg;
        public long BlockCapacity => DefaultBlockCapacity;
        public SEMemoryStreamMode SEMSMode => LRSM;

        public void UnlockStream()
        {
            if (LRSM == SEMemoryStreamMode.Fixed)
            {
                LRSM = SEMemoryStreamMode.Expandable;
                long l = mss[nco - 1].Length;
                if (l < DefaultBlockCapacity)
                {
                    byte[] b = mss[nco - 1].ToArray();
                    mss[nco - 1].Dispose();
                    mss[nco - 1] = new SEMemoryStreamSlim(DefaultBlockCapacity_int32);
                    mss[nco - 1].Write(b);
                    b = null;
                    GC.Collect();
                    tplg = tplg - l + DefaultBlockCapacity;
                }
            }
            lcv = LRSM == SEMemoryStreamMode.Fixed;
        }

        ~SEMemoryStream()
        {
            for (long i = 0; i < nco; i++)
            {
                mss[i].Dispose();
            }
        }

        public SEMemoryStream(SEMemoryStreamMode LRSM = SEMemoryStreamMode.Expandable, SEBlockSize LRBS = SEBlockSize.Minimum)
        {
            if (LRBS == SEBlockSize.Auto)
            {
                LRBS = SEBlockSize.Minimum;
            }
            this.LRSM = LRSM;
            lcv = LRSM == SEMemoryStreamMode.Fixed;
            DefaultBlockCapacity = (long)LRBS;
            DefaultBlockCapacity_int32 = (int)DefaultBlockCapacity;
            AddMS(DefaultBlockCapacity_int32);
        }

        void AddMS(int c)
        {
            mss = mss.Append(new SEMemoryStreamSlim(c)).ToArray();
            nco = mss.LongLength;
            tplg += c;
        }
        private static SEBlockSize DetermineBlockSize(long capacity)
        {
            // 若容量极小（如小于1MB），直接返回 Minimum 或更小的块，避免过度碎片化
            if (capacity <= (long)SEBlockSize.Minimum)
                return SEBlockSize.Minimum;

            // 所有可用的块大小（除 Auto 外），按从小到大排序以便逻辑清晰
            var allBlockSizes = new[]
            {
                SEBlockSize.Stream,
                SEBlockSize.Buffer,
                SEBlockSize.Cache,
                SEBlockSize.TempPool,
                SEBlockSize.Special,
                SEBlockSize.Minimum,
                SEBlockSize.VerySmall,
                SEBlockSize.Small,
                SEBlockSize.Normal,
                SEBlockSize.Large,
                SEBlockSize.VeryLarge,
                SEBlockSize.Maximal,
                SEBlockSize.Ocean
            };

            // 目标块数范围（可根据实际场景调整）
            const int targetMinBlocks = 16;
            const int targetMaxBlocks = 64;

            SEBlockSize best = SEBlockSize.Minimum;
            long bestBlockCount = long.MaxValue;
            long bestWaste = long.MaxValue; // 用于当块数均不符合区间时的次要评判指标

            foreach (var blockSize in allBlockSizes)
            {
                long blockSizeValue = (long)blockSize;
                long blockCount = (capacity + blockSizeValue - 1) / blockSizeValue;
                long waste = blockCount * blockSizeValue - capacity;

                // 优先选择块数在目标区间内的块大小
                if (blockCount >= targetMinBlocks && blockCount <= targetMaxBlocks)
                {
                    // 若已有符合条件的，选择浪费更少的（即块大小更小的）
                    if (bestBlockCount < targetMinBlocks || bestBlockCount > targetMaxBlocks ||
                        waste < bestWaste)
                    {
                        best = blockSize;
                        bestBlockCount = blockCount;
                        bestWaste = waste;
                    }
                }
                // 记录最接近目标区间的（用于无完全符合时）
                else if (bestBlockCount == long.MaxValue ||
                         Math.Abs(blockCount - targetMinBlocks) < Math.Abs(bestBlockCount - targetMinBlocks))
                {
                    best = blockSize;
                    bestBlockCount = blockCount;
                    bestWaste = waste;
                }
            }

            return best;
        }
        public SEMemoryStream(long Capacity, SEMemoryStreamMode LRSM = SEMemoryStreamMode.Fixed, SEBlockSize LRBS = SEBlockSize.Auto)
        {
            if (LRBS == SEBlockSize.Auto)
            {
                LRBS = DetermineBlockSize(Capacity);
            }
            this.LRSM = LRSM;
            lcv = LRSM == SEMemoryStreamMode.Fixed;
            DefaultBlockCapacity = (long)LRBS;
            DefaultBlockCapacity_int32 = (int)DefaultBlockCapacity;
            long c = Capacity / DefaultBlockCapacity;
            if (Capacity % DefaultBlockCapacity != 0)
            {
                c++;
            }
            for (long i = 0; i < c; i++)
            {
                AddMS(DefaultBlockCapacity_int32);
            }
            lg = Capacity;
            psi = 0;
            ndi = 0;
        }

        public override void Flush()
        {
            for (long i = 0; i < nco; i++)
            {
                mss[i].Flush();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count <= 0) return 0;
            if (psi >= lg) return 0;

            // 确保不读取超过流末尾
            int bytesToRead = (int)Math.Min(count, lg - psi);
            int totalBytesRead = 0;

            while (bytesToRead > 0)
            {
                // 计算当前块索引和块内偏移
                long currentBlockIndex = psi / DefaultBlockCapacity;
                long blockOffset = psi % DefaultBlockCapacity;

                // 确保块索引有效
                if (currentBlockIndex >= nco)
                    break;

                // 计算当前块中可读取的字节数
                int bytesAvailableInBlock = (int)Math.Min(DefaultBlockCapacity - blockOffset, bytesToRead);

                // 定位并读取
                mss[currentBlockIndex].Position = blockOffset;
                int bytesRead = mss[currentBlockIndex].Read(buffer, offset, bytesAvailableInBlock);

                if (bytesRead == 0) break; // 没有更多数据可读

                // 更新位置和计数器
                psi += bytesRead;
                offset += bytesRead;
                totalBytesRead += bytesRead;
                bytesToRead -= bytesRead;
            }

            return totalBytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = psi + offset;
                    break;
                case SeekOrigin.End:
                    newPosition = lg + offset;
                    break;
                default:
                    throw new ArgumentException("Invalid seek origin");
            }

            if (newPosition < 0)
                throw new IOException("企图跳转到流的开始之前".GetInCurrLang());

            if (lcv && newPosition > lg)
                throw new IndexOutOfRangeException("方位超出流的范围".GetInCurrLang());

            psi = newPosition;
            ndi = psi / DefaultBlockCapacity;

            // 如果需要扩展流
            if (!lcv && ndi >= nco)
            {
                long blocksNeeded = ndi - nco + 1;
                for (long i = 0; i < blocksNeeded; i++)
                {
                    AddMS(DefaultBlockCapacity_int32);
                }
            }

            return psi;
        }

        public override void SetLength(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "长度不可为负".GetInCurrLang());

            if (lcv && value > lg)
                throw new InvalidOperationException("不可调整固定的流".GetInCurrLang());

            long blocksNeeded = (value + DefaultBlockCapacity - 1) / DefaultBlockCapacity;

            // 调整块数量
            if (blocksNeeded > nco)
            {
                for (long i = nco; i < blocksNeeded; i++)
                {
                    AddMS(DefaultBlockCapacity_int32);
                }
            }
            else if (blocksNeeded < nco)
            {
                // 移除多余的块
                for (long i = nco - 1; i >= blocksNeeded; i--)
                {
                    mss[i].Dispose();
                }
                Array.Resize(ref mss, (int)blocksNeeded);
                nco = blocksNeeded;
            }

            lg = value;
            if (psi > lg)
                psi = lg;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count <= 0) return;
            if (lcv && psi + count > lg)
                throw new IndexOutOfRangeException("不可写超出固定流".GetInCurrLang());

            int bytesToWrite = count;

            while (bytesToWrite > 0)
            {
                // 计算当前块索引和块内偏移
                long currentBlockIndex = psi / DefaultBlockCapacity;
                long blockOffset = psi % DefaultBlockCapacity;

                // 如果需要扩展流
                if (currentBlockIndex >= nco)
                {
                    if (lcv)
                        throw new IndexOutOfRangeException("不可调整固定的流".GetInCurrLang());

                    long blocksNeeded = currentBlockIndex - nco + 1;
                    for (long i = 0; i < blocksNeeded; i++)
                    {
                        AddMS(DefaultBlockCapacity_int32);
                    }
                }

                // 计算当前块中可写入的字节数
                int bytesAvailableInBlock = (int)Math.Min(DefaultBlockCapacity - blockOffset, bytesToWrite);

                // 定位并写入
                mss[currentBlockIndex].Position = blockOffset;
                mss[currentBlockIndex].Write(buffer, offset, bytesAvailableInBlock);

                // 更新位置和计数器
                psi += bytesAvailableInBlock;
                offset += bytesAvailableInBlock;
                bytesToWrite -= bytesAvailableInBlock;

                // 更新流长度（如果是可扩展的）
                if (!lcv && psi > lg)
                {
                    lg = psi;
                }
            }
        }
    }
}
