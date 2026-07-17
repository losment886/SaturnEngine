using Avalonia.Animation.Easings;
using SaturnEngine.Asset;
using System.Runtime.InteropServices;
using System.Threading;
using static SaturnEngine.Management.SEMemory.SEMemoryStream;

namespace SaturnEngine.Management.SEMemory
{

    public class SEMemoryStreamMT : Stream
    {

        long DefaultBlockCapacity = 1048576;//1MB
        int DefaultBlockCapacity_int32 = 1048576;//1MB
        long leng = 0;
        long currblocount = 0;

        long memusg = 0; // 以已分配块容量总和为统计口径

        readonly List<BlockEntry> blocks = new List<BlockEntry>();
        readonly ThreadLocal<long> threadpsi = new ThreadLocal<long>(() => 0);

        // 保护结构变更（扩容/缩容/替换块列表）的全局锁
        private readonly object _structureLock = new object();

        // 释放状态标志
        private bool _disposed = false;


        SEMemoryStream.SEMemoryStreamMode LRSM;
        bool lcv = false;


        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => Interlocked.Read(ref leng);

        public override long Position { get => GetPosition(); set => Seek(value, SeekOrigin.Begin); }

        public long MemoryUsage => Interlocked.Read(ref memusg);
        public long BlockCapacity => Interlocked.Read(ref DefaultBlockCapacity);
        public SEMemoryStream.SEMemoryStreamMode SEMSMode => LRSM;

        sealed class BlockEntry
        {
            public BlockEntry(SEMemoryStreamSlim stream, int capacity)
            {
                Stream = stream;
                Capacity = capacity;
            }

            public SEMemoryStreamSlim? Stream;
            public readonly object SyncRoot = new object();
            public int ActiveUsers;
            public bool PendingDispose;
            public int Capacity;
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SEMemoryStreamMT));
            
        }

        BlockEntry CreateBlockEntry(int capacity)
        {
            return new BlockEntry(new SEMemoryStreamSlim(capacity), capacity);
        }

        void AddBlockCore(int capacity)
        {
            var block = CreateBlockEntry(capacity);
            blocks.Add(block);
            Interlocked.Add(ref memusg, block.Capacity);
            Interlocked.Exchange(ref currblocount, blocks.Count);
        }

        void EnsureBlockExistsCore(int blockIndex)
        {
            if (blockIndex < blocks.Count)
                return;

            if (lcv)
                throw new NotSupportedException("不可调整固定的流".GetInCurrLang());

            int blocksNeeded = blockIndex - blocks.Count + 1;
            for (int i = 0; i < blocksNeeded; i++)
            {
                AddBlockCore(DefaultBlockCapacity_int32);
            }
        }

        bool TryAcquireBlock(int blockIndex, out BlockEntry? block)
        {
            lock (_structureLock)
            {
                ThrowIfDisposed();
                if (blockIndex < 0 || blockIndex >= blocks.Count)
                {
                    block = null;
                    return false;
                }

                block = blocks[blockIndex];
                block.ActiveUsers++;
                return true;
            }
        }

        BlockEntry AcquireWritableBlock(int blockIndex)
        {
            lock (_structureLock)
            {
                ThrowIfDisposed();
                EnsureBlockExistsCore(blockIndex);
                var block = blocks[blockIndex];
                block.ActiveUsers++;
                return block;
            }
        }

        void ReleaseBlock(BlockEntry block)
        {
            SEMemoryStreamSlim? streamToDispose = null;
            lock (_structureLock)
            {
                if (block.ActiveUsers > 0)
                {
                    block.ActiveUsers--;
                }

                if (block.PendingDispose && block.ActiveUsers == 0 && block.Stream != null)
                {
                    streamToDispose = block.Stream;
                    block.Stream = null;
                    block.PendingDispose = false;
                }
            }

            if (streamToDispose != null)
            {
                streamToDispose.Close();
            }
        }

        void MarkBlockPendingDisposeCore(BlockEntry block, List<SEMemoryStreamSlim>? disposeNow = null)
        {
            block.PendingDispose = true;
            if (block.ActiveUsers == 0 && block.Stream != null)
            {
                if (disposeNow != null)
                {
                    disposeNow.Add(block.Stream);
                }
                block.Stream = null;
                block.PendingDispose = false;
            }
        }

        List<BlockEntry> SnapshotAllVisibleBlocks()
        {
            var snapshot = new List<BlockEntry>();
            lock (_structureLock)
            {
                ThrowIfDisposed();
                foreach (var block in blocks)
                {
                    if (block.Stream == null)
                        continue;

                    block.ActiveUsers++;
                    snapshot.Add(block);
                }
            }

            return snapshot;
        }


        /// <summary>
        /// 线程安全地把 lg 更新为 max(lg, newPosition)。
        /// 只在新位置更大时更新，避免并发写入互相覆盖导致长度回退。
        /// </summary>
        void UpdateLength(long newPosition)
        {
            // 先用 Volatile/Interlocked 读取当前值（long 在 32 位平台上非原子）
            long current = Interlocked.Read(ref leng);

            while (newPosition > current)
            {
                // 尝试：如果 lg 仍然等于 current，就替换为 newPosition
                long original = Interlocked.CompareExchange(ref leng, newPosition, current);

                if (original == current)
                {
                    // 替换成功，退出
                    break;
                }

                // 替换失败：说明其他线程刚改过 lg，
                // original 就是最新值，用它重新比较再试
                current = original;
            }
            // 循环退出条件：要么替换成功，要么别的线程已写入了更大的长度（newPosition <= current），无需再更新
        }


        public void UnlockStream()
        {
            ThrowIfDisposed();

            BlockEntry? oldLastBlock = null;
            SEMemoryStreamSlim? oldLastStream = null;
            long oldLastLength = 0;
            lock (_structureLock)
            {
                if (LRSM == SEMemoryStream.SEMemoryStreamMode.Fixed)
                {
                    LRSM = SEMemoryStream.SEMemoryStreamMode.Expandable;
                    lcv = false;
                    if (blocks.Count > 0)
                    {
                        oldLastBlock = blocks[blocks.Count - 1];
                        oldLastStream = oldLastBlock.Stream;
                        if (oldLastStream != null)
                        {
                            oldLastLength = oldLastStream.Length;
                            if (oldLastLength < DefaultBlockCapacity)
                            {
                                oldLastBlock.ActiveUsers++;
                            }
                            else
                            {
                                oldLastBlock = null;
                            }
                        }
                    }
                }
                else
                {
                    lcv = LRSM == SEMemoryStream.SEMemoryStreamMode.Fixed;
                    return;
                }
            }

            if (oldLastBlock == null || oldLastStream == null)
                return;

            byte[] buffer;
            lock (oldLastBlock.SyncRoot)
            {
                buffer = oldLastStream.ToArray();
            }

            var replacement = CreateBlockEntry(DefaultBlockCapacity_int32);
            lock (replacement.SyncRoot)
            {
                replacement.Stream!.Write(buffer, 0, buffer.Length);
            }

            SEMemoryStreamSlim? streamToDispose = null;
            lock (_structureLock)
            {
                if (blocks.Count > 0 && ReferenceEquals(blocks[blocks.Count - 1], oldLastBlock))
                {
                    blocks[blocks.Count - 1] = replacement;
                    Interlocked.Add(ref memusg, replacement.Capacity - oldLastBlock.Capacity);
                    MarkBlockPendingDisposeCore(oldLastBlock);
                }

                if (oldLastBlock.ActiveUsers > 0)
                {
                    oldLastBlock.ActiveUsers--;
                }

                if (oldLastBlock.PendingDispose && oldLastBlock.ActiveUsers == 0 && oldLastBlock.Stream != null)
                {
                    streamToDispose = oldLastBlock.Stream;
                    oldLastBlock.Stream = null;
                    oldLastBlock.PendingDispose = false;
                }
            }

            streamToDispose?.Close();
        }


        void AddMS(int c)
        {
            ThrowIfDisposed();
            lock (_structureLock)
            {
                AddBlockCore(c);
            }
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
                SEBlockSize.Maximal
            };

            // 目标块数范围（可根据实际场景调整）
            const int targetMinBlocks = 8;
            const int targetMaxBlocks = 512;

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

        public override void Flush()
        {
            var snapshot = SnapshotAllVisibleBlocks();
            try
            {
                foreach (var block in snapshot)
                {
                    lock (block.SyncRoot)
                    {
                        block.Stream?.Flush();
                    }
                }
            }
            finally
            {
                foreach (var block in snapshot)
                {
                    ReleaseBlock(block);
                }
            }
        }



        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException();

            long psi = GetPosition();
            if (count <= 0) return 0;
            if (psi >= Length) return 0;
            
            // 确保不读取超过流末尾
            int bytesToRead = (int)Math.Min(count, Length - psi);
            int totalBytesRead = 0;

            while (bytesToRead > 0)
            {
                int currentBlockIndex = (int)(psi / DefaultBlockCapacity);
                int blockOffset = (int)(psi % DefaultBlockCapacity);
                int bytesAvailableInBlock = (int)Math.Min(DefaultBlockCapacity - blockOffset, bytesToRead);
                int bytesRead = 0;

                if (!TryAcquireBlock(currentBlockIndex, out BlockEntry? block) || block == null)
                {
                    break;
                }

                try
                {
                    lock (block.SyncRoot)
                    {
                        var stream = block.Stream ?? throw new ObjectDisposedException(nameof(SEMemoryStreamSlim));
                        stream.Position = blockOffset;
                        bytesRead = stream.Read(buffer, offset, bytesAvailableInBlock);
                    }
                }
                finally
                {
                    ReleaseBlock(block);
                }

                if (bytesRead == 0) break; // 没有更多数据可读

                // 更新位置和计数器
                psi += bytesRead;
                offset += bytesRead;
                totalBytesRead += bytesRead;
                bytesToRead -= bytesRead;
            }
            SetPosition(psi);
            return totalBytesRead;
        }

        public long GetPosition()
        {
            return threadpsi.Value;
        }
        public void SetPosition(long value)
        {
            threadpsi.Value = value;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            long psi = GetPosition();
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
                    newPosition = Length + offset;
                    break;
                default:
                    throw new ArgumentException("Invalid seek origin");
            }

            if (newPosition < 0)
                throw new IOException("企图跳转到流的开始之前".GetInCurrLang());

            if (lcv && newPosition > Length)
                throw new IndexOutOfRangeException("方位超出流的范围".GetInCurrLang());

            psi = newPosition;
            long ndi = psi / DefaultBlockCapacity;

            if (!lcv)
            {
                lock (_structureLock)
                {
                    if (ndi >= blocks.Count)
                    {
                        EnsureBlockExistsCore((int)ndi);
                    }
                }
            }
            
            SetPosition(psi);
            return psi;
        }

        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "长度不可为负".GetInCurrLang());

            if (lcv && value > Length)
                throw new NotSupportedException("不可调整固定的流".GetInCurrLang());

            var disposeNow = new List<SEMemoryStreamSlim>();
            lock (_structureLock)
            {
                long blocksNeeded = (value + DefaultBlockCapacity - 1) / DefaultBlockCapacity;
                if (blocksNeeded == 0) blocksNeeded = 1; // 至少保留一块

                if (blocksNeeded > blocks.Count)
                {
                    for (long i = blocks.Count; i < blocksNeeded; i++)
                    {
                        AddBlockCore(DefaultBlockCapacity_int32);
                    }
                }
                else if (blocksNeeded < blocks.Count)
                {
                    for (int i = blocks.Count - 1; i >= (int)blocksNeeded; i--)
                    {
                        var block = blocks[i];
                        blocks.RemoveAt(i);
                        Interlocked.Add(ref memusg, -block.Capacity);
                        MarkBlockPendingDisposeCore(block, disposeNow);
                    }
                }
                Interlocked.Exchange(ref currblocount, blocks.Count);
                Interlocked.Exchange(ref leng, value);
            }

            foreach (var stream in disposeNow)
            {
                stream.Close();
            }

            long psi = GetPosition();
            if (psi > value)
                SetPosition(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException();

            long psi = GetPosition();
            if (count <= 0) return;
            if (lcv && psi + count > Length)
                throw new NotSupportedException("不可写超出固定流".GetInCurrLang());

            int bytesToWrite = count;

            while (bytesToWrite > 0)
            {
                int currentBlockIndex = (int)(psi / DefaultBlockCapacity);
                int blockOffset = (int)(psi % DefaultBlockCapacity);
                int bytesAvailableInBlock = (int)Math.Min(DefaultBlockCapacity - blockOffset, bytesToWrite);

                var block = AcquireWritableBlock(currentBlockIndex);
                try
                {
                    lock (block.SyncRoot)
                    {
                        var stream = block.Stream ?? throw new ObjectDisposedException(nameof(SEMemoryStreamSlim));
                        stream.Position = blockOffset;
                        stream.Write(buffer, offset, bytesAvailableInBlock);
                    }
                }
                finally
                {
                    ReleaseBlock(block);
                }

                psi += bytesAvailableInBlock;
                offset += bytesAvailableInBlock;
                bytesToWrite -= bytesAvailableInBlock;

                if (!lcv && psi > Length)
                {
                    UpdateLength(psi);
                }
            }
            SetPosition(psi);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                base.Dispose(disposing);
                return;
            }

            var disposeNow = new List<SEMemoryStreamSlim>();
            lock (_structureLock)
            {
                if (_disposed)
                {
                    base.Dispose(disposing);
                    return;
                }

                _disposed = true;
                foreach (var block in blocks)
                {
                    MarkBlockPendingDisposeCore(block, disposeNow);
                }
                blocks.Clear();
                Interlocked.Exchange(ref currblocount, 0);
                Interlocked.Exchange(ref memusg, 0);
            }

            foreach (var stream in disposeNow)
            {
                try
                {
                    stream.Close();
                }
                catch
                {
                }
            }

            if (disposing)
            {
                try
                {
                    threadpsi.Dispose();
                }
                catch
                {
                }
            }

            base.Dispose(disposing);
        }

        public SEMemoryStreamMT(SEMemoryStreamMode LRSM = SEMemoryStreamMode.Expandable, SEBlockSize LRBS = SEBlockSize.Minimum)
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
        public SEMemoryStreamMT(long Capacity, SEMemoryStreamMode LRSM = SEMemoryStreamMode.Fixed, SEBlockSize LRBS = SEBlockSize.Auto)
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
            leng = Capacity;
            //psi = 0;
            //ndi = 0;
        }
    }






    /// <summary>
    /// 一个可扩展的内存流，支持固定长度和可扩展两种模式，并提供块大小配置选项。
    /// 注意：此为非线程安全的实现，多线程同时读写请使用 <see cref="SEMemoryStreamMT"/>，其余自行加锁。
    /// </summary>
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
            VeryLarge = 536870912,//512MB
            /// <summary>
            /// 1GB块大小
            /// </summary>
            Maximal = 1073741824,//1GB
            /// <summary>
            /// 仅用于自动设置块大小
            /// </summary>
            Auto = 1145141919810//仅限于有给定长度的流，会自动确认大小，否则为Minimum，大小不可更改，除非重置
        }

        long DefaultBlockCapacity = 1048576;//1MB
        int DefaultBlockCapacity_int32 = 1048576;//1MB
        long leng = 0;
        long psi = 0;
        long ndi = 0;
        long currblocou = 0;

        long memusg = 0;

        SEMemoryStreamSlim[] mss = new SEMemoryStreamSlim[0];
        SEMemoryStreamMode LRSM;
        bool lcv = false;
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => leng;

        public override long Position { get => psi; set => Seek(value, SeekOrigin.Begin); }

        public long MemoryUsage => memusg;
        public long BlockCapacity => DefaultBlockCapacity;
        public SEMemoryStreamMode SEMSMode => LRSM;

        public void UnlockStream()
        {
            if (LRSM == SEMemoryStreamMode.Fixed)
            {
                LRSM = SEMemoryStreamMode.Expandable;
                long l = mss[currblocou - 1].Length;
                if (l < DefaultBlockCapacity)
                {
                    byte[] b = mss[currblocou - 1].ToArray();
                    mss[currblocou - 1].Dispose();
                    mss[currblocou - 1] = new SEMemoryStreamSlim(DefaultBlockCapacity_int32);
                    mss[currblocou - 1].Write(b);
                    b = null;
                    GC.Collect();
                    memusg = memusg - l + DefaultBlockCapacity;
                }
            }
            lcv = LRSM == SEMemoryStreamMode.Fixed;
        }

        ~SEMemoryStream()
        {
            for (long i = 0; i < currblocou; i++)
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
            currblocou = mss.LongLength;
            memusg += c;
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
                SEBlockSize.Maximal
            };

            // 目标块数范围（可根据实际场景调整）
            const int targetMinBlocks = 8;
            const int targetMaxBlocks = 512;

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
            leng = Capacity;
            psi = 0;
            ndi = 0;
        }

        public override void Flush()
        {
            for (long i = 0; i < currblocou; i++)
            {
                mss[i].Flush();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count <= 0) return 0;
            if (psi >= leng) return 0;

            // 确保不读取超过流末尾
            int bytesToRead = (int)Math.Min(count, leng - psi);
            int totalBytesRead = 0;

            while (bytesToRead > 0)
            {
                // 计算当前块索引和块内偏移
                long currentBlockIndex = psi / DefaultBlockCapacity;
                long blockOffset = psi % DefaultBlockCapacity;

                // 确保块索引有效
                if (currentBlockIndex >= currblocou)
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
                    newPosition = leng + offset;
                    break;
                default:
                    throw new ArgumentException("Invalid seek origin");
            }

            if (newPosition < 0)
                throw new IOException("企图跳转到流的开始之前".GetInCurrLang());

            if (lcv && newPosition > leng)
                throw new IndexOutOfRangeException("方位超出流的范围".GetInCurrLang());

            psi = newPosition;
            ndi = psi / DefaultBlockCapacity;

            // 如果需要扩展流
            if (!lcv && ndi >= currblocou)
            {
                long blocksNeeded = ndi - currblocou + 1;
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

            if (lcv && value > leng)
                throw new InvalidOperationException("不可调整固定的流".GetInCurrLang());

            long blocksNeeded = (value + DefaultBlockCapacity - 1) / DefaultBlockCapacity;

            // 调整块数量
            if (blocksNeeded > currblocou)
            {
                for (long i = currblocou; i < blocksNeeded; i++)
                {
                    AddMS(DefaultBlockCapacity_int32);
                }
            }
            else if (blocksNeeded < currblocou)
            {
                // 移除多余的块
                for (long i = currblocou - 1; i >= blocksNeeded; i--)
                {
                    mss[i].Dispose();
                }
                Array.Resize(ref mss, (int)blocksNeeded);
                currblocou = blocksNeeded;
            }

            leng = value;
            if (psi > leng)
                psi = leng;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count <= 0) return;
            if (lcv && psi + count > leng)
                throw new IndexOutOfRangeException("不可写超出固定流".GetInCurrLang());

            int bytesToWrite = count;

            while (bytesToWrite > 0)
            {
                // 计算当前块索引和块内偏移
                long currentBlockIndex = psi / DefaultBlockCapacity;
                long blockOffset = psi % DefaultBlockCapacity;

                // 如果需要扩展流
                if (currentBlockIndex >= currblocou)
                {
                    if (lcv)
                        throw new IndexOutOfRangeException("不可调整固定的流".GetInCurrLang());

                    long blocksNeeded = currentBlockIndex - currblocou + 1;
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
                if (!lcv && psi > leng)
                {
                    leng = psi;
                }
            }
        }
    }
}
