using SaturnEngine.Global;
using SaturnEngine.Performance;
using SixLabors.Fonts.Tables.AdvancedTypographic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SaturnEngine.Security
{
    /// <summary>
    /// 提供一个容器，用于存放需要被高度安全保护的敏感数据。可实现短时存放
    /// </summary>
    internal unsafe class SEMSV : IDisposable
    {
        static bool inited = false;
        static SEThread? mainT;
        static bool Running = false;

        // 全局数据结构
        private static readonly ConcurrentDictionary<Guid, SEMSV> _instances = new();
        private static readonly ConcurrentDictionary<ulong, (SEMSV Instance, int ThreadId)> _ownerTokens = new();
        private static readonly ConcurrentDictionary<ulong, (SEMSV Instance, int TargetThreadId, ulong OriginalToken, long ExpireTicks)> _transferTokens = new();
        private static readonly ConcurrentDictionary<ulong, (SEMSV Instance, int ThreadId, int RemainingUses)> _guestTokens = new();

        // Token 生成辅助
        private static ulong GenerateUniqueToken<T>(ConcurrentDictionary<ulong, T> dictionary)
        {
            Span<byte> buffer = stackalloc byte[8];
            ulong token;
            do
            {
                RandomNumberGenerator.Fill(buffer);
                token = BitConverter.ToUInt64(buffer);
            } while (token == 0 || dictionary.ContainsKey(token));
            return token;
        }
        /// <summary>
        /// 初始化全局管理器。该方法必须在程序启动时调用一次，且必须在任何SEMSV实例创建之前调用。默认在EngineInit中调用。
        /// </summary>
        internal static void Init()
        {
            if(inited) return;
            inited = true;
            if(mainT != null)
            {
                Running = false;
                Dispatcher.Delay(0.250);
                mainT.Dispose();
                mainT = null;
            }
            Running = true;
            mainT = Dispatcher.CreateThread(Worker, ThreadPriority.Normal);
            GVariables.OnEngineClose += Close;
        }
        internal static void Close()
        {
            inited = false;
            Running = false;
            if (mainT != null)
            {
                Dispatcher.Delay(0.250);
                mainT.Dispose();
                mainT = null;
            }
            // 清空全局表
            _instances.Clear();
            _ownerTokens.Clear();
            _transferTokens.Clear();
            _guestTokens.Clear();
        }

        static void Worker()
        {
            mainT.SetFPS(25);

            while(Running)
            {
                long nowTicks = DateTime.UtcNow.Ticks;

                // 1. TTL 过期检查与清理
                foreach (var kvp in _instances)
                {
                    var instance = kvp.Value;
                    if (instance._expireTicks != 0 && nowTicks > instance._expireTicks)
                    {
                        instance.Dispose();
                    }
                }

                // 2. 定时 rekey 转移
                foreach (var kvp in _instances)
                {
                    var instance = kvp.Value;
                    if (instance._disposed != 0) continue;

                    if (nowTicks >= instance._nextRelocateTicks)
                    {
                        try
                        {
                            instance._lock.EnterWriteLock();
                            try
                            {
                                // 解密到临时缓冲
                                byte* tempPlain = (byte*)NativeMemory.Alloc((nuint)instance.length);
                                var tempSpan = new Span<byte>(tempPlain, instance.length);
                                try
                                {
                                    using (var gcm = new AesGcm(new ReadOnlySpan<byte>(instance.key, 32), 16))
                                    {
                                        gcm.Decrypt(instance._nonce, new ReadOnlySpan<byte>(instance.data, instance.length), instance._tag, tempSpan);
                                    }

                                    // 生成新 key 和 nonce
                                    byte* newKey = (byte*)NativeMemory.Alloc(32);
                                    RandomNumberGenerator.Fill(new Span<byte>(newKey, 32));
                                    RandomNumberGenerator.Fill(instance._nonce);

                                    // 分配新内存并加密
                                    byte* newData = (byte*)NativeMemory.Alloc((nuint)instance.length);
                                    using (var gcm = new AesGcm(new ReadOnlySpan<byte>(newKey, 32), 16))
                                    {
                                        gcm.Encrypt(instance._nonce, tempSpan, new Span<byte>(newData, instance.length), instance._tag);
                                    }

                                    // 清零释放旧块
                                    CryptographicOperations.ZeroMemory(new Span<byte>(instance.key, 32));
                                    NativeMemory.Free(instance.key);
                                    CryptographicOperations.ZeroMemory(new Span<byte>(instance.data, instance.length));
                                    NativeMemory.Free(instance.data);

                                    // 更新指针
                                    instance.key = newKey;
                                    instance.data = newData;

                                    // 更新下次转移时间（30秒后）
                                    instance._nextRelocateTicks = DateTime.UtcNow.AddSeconds(30).Ticks;
                                }
                                finally
                                {
                                    CryptographicOperations.ZeroMemory(tempSpan);
                                    NativeMemory.Free(tempPlain);
                                }
                            }
                            finally
                            {
                                instance._lock.ExitWriteLock();
                            }
                        }
                        catch
                        {
                            // 忽略错误，下次继续尝试
                        }
                    }
                }

                // 3. 清理过期的验证 token
                var expiredTransferTokens = _transferTokens
                    .Where(kvp => nowTicks > kvp.Value.ExpireTicks)
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var token in expiredTransferTokens)
                {
                    _transferTokens.TryRemove(token, out _);
                }

                mainT.WaitForFPS();
            }
        }



        private byte* data;
        private int length;
        private byte* key;

        // 改为可变（转移时换绑）
        private int _ownerThreadId;
        private long _expireTicks;
        private byte[] _nonce = new byte[12];
        private byte[] _tag = new byte[16];

        // 新增字段
        private readonly Guid _instanceId = Guid.NewGuid();
        private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
        internal long _nextRelocateTicks;
        private ulong _currentOwnerToken;
        private bool _initialized;

        internal delegate void ReadOnlySpanAction<T>(Span<T> span);

        public SEMSV()
        {
            _ownerThreadId = Environment.CurrentManagedThreadId;
            
        }

        /// <summary>
        /// 初始化实例并返回权限 token，只能由创建线程调用一次
        /// </summary>
        public ulong Init(Span<byte> plaintext, TimeSpan? ttl = null)
        {

            ObjectDisposedException.ThrowIf(_disposed != 0, this);

            if (_initialized)
                throw new InvalidOperationException("实例已初始化，不能重复调用 Init()");

            if (Environment.CurrentManagedThreadId != _ownerThreadId)
                throw new UnauthorizedAccessException("只有创建该容器的线程可以调用 Init()");

            _expireTicks = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value).Ticks : 0;

            key = (byte*)NativeMemory.Alloc(32);
            RandomNumberGenerator.Fill(new Span<byte>(key, 32));
            RandomNumberGenerator.Fill(_nonce);

            length = plaintext.Length;
            data = (byte*)NativeMemory.Alloc((nuint)length);

            using var gcm = new AesGcm(new ReadOnlySpan<byte>(key, 32), 16);
            gcm.Encrypt(_nonce, plaintext, new Span<byte>(data, length), _tag);

            CryptographicOperations.ZeroMemory(plaintext);

            // 计算首次 rekey 时间（构造后 30 秒）
            _nextRelocateTicks = DateTime.UtcNow.AddSeconds(30).Ticks;


            _initialized = true;
            _currentOwnerToken = GenerateUniqueToken(_ownerTokens);

            _instances.TryAdd(_instanceId, this);
            _ownerTokens.TryAdd(_currentOwnerToken, (this, _ownerThreadId));

            return _currentOwnerToken;
        }

        /// <summary>
        /// 使用 token 读取数据（支持权限 token 和访客 token，并发安全）
        /// </summary>
        public void Read(ulong token, ReadOnlySpanAction<byte> action)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);

            // 校验 token 类型与线程绑定
            bool isOwner = false;
            bool isGuest = false;
            int currentThreadId = Environment.CurrentManagedThreadId;
            (SEMSV Instance, int ThreadId, int RemainingUses) guestInfo = default;

            if (_ownerTokens.TryGetValue(token, out var ownerInfo) && ownerInfo.Instance == this)
            {
                if (ownerInfo.ThreadId != currentThreadId)
                    throw new UnauthorizedAccessException("权限 token 只能由绑定的线程使用");
                isOwner = true;
            }
            else if (_guestTokens.TryGetValue(token, out guestInfo) && guestInfo.Instance == this)
            {
                if (guestInfo.ThreadId != currentThreadId)
                    throw new UnauthorizedAccessException("访客 token 只能由绑定的线程使用");
                isGuest = true;
            }
            else
            {
                throw new UnauthorizedAccessException("无效的 token 或 token 不属于此实例");
            }

            // TTL 校验
            if (_expireTicks != 0 && DateTime.UtcNow.Ticks > _expireTicks)
            {
                Dispose();
                throw new UnauthorizedAccessException("容器已过期，内容已被销毁。");
            }

            // 访客 token 递减次数
            if (isGuest)
            {
                var (inst, tid, remaining) = guestInfo;
                int newRemaining = Interlocked.Decrement(ref remaining);
                if (newRemaining < 0)
                {
                    _guestTokens.TryRemove(token, out _);
                    throw new UnauthorizedAccessException("访客 token 使用次数已用尽");
                }
                _guestTokens[token] = (inst, tid, newRemaining);
                if (newRemaining == 0)
                    _guestTokens.TryRemove(token, out _);
            }

            // 读锁保护解密操作
            _lock.EnterReadLock();
            try
            {
                byte* plain = (byte*)NativeMemory.Alloc((nuint)length);
                var plainSpan = new Span<byte>(plain, length);
                try
                {
                    using var gcm = new AesGcm(new ReadOnlySpan<byte>(key, 32), 16);
                    gcm.Decrypt(_nonce, new ReadOnlySpan<byte>(data, length), _tag, plainSpan);
                    action(plainSpan);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plainSpan);
                    NativeMemory.Free(plain);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 修改容器数据（仅权限 token 可用，单线程）
        /// </summary>
        public void Modify(ulong ownerToken, Span<byte> newPlaintext)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);

            // 仅权限 token
            if (!_ownerTokens.TryGetValue(ownerToken, out var ownerInfo) || ownerInfo.Instance != this)
                throw new UnauthorizedAccessException("无效的权限 token 或 token 不属于此实例");

            if (ownerInfo.ThreadId != Environment.CurrentManagedThreadId)
                throw new UnauthorizedAccessException("权限 token 只能由绑定的线程使用");

            // TTL 校验
            if (_expireTicks != 0 && DateTime.UtcNow.Ticks > _expireTicks)
            {
                Dispose();
                throw new UnauthorizedAccessException("容器已过期，内容已被销毁。");
            }

            // 写锁
            _lock.EnterWriteLock();
            try
            {
                // 释放旧数据
                if (data != null)
                {
                    CryptographicOperations.ZeroMemory(new Span<byte>(data, length));
                    NativeMemory.Free(data);
                }

                // 更新长度并分配新内存
                length = newPlaintext.Length;
                data = (byte*)NativeMemory.Alloc((nuint)length);

                // 生成新 nonce 并加密
                RandomNumberGenerator.Fill(_nonce);
                using var gcm = new AesGcm(new ReadOnlySpan<byte>(key, 32), 16);
                gcm.Encrypt(_nonce, newPlaintext, new Span<byte>(data, length), _tag);
            }
            finally
            {
                _lock.ExitWriteLock();
                // 清零入参明文
                CryptographicOperations.ZeroMemory(newPlaintext);
            }
        }

        /// <summary>
        /// 开始转移：所有者线程传入目标线程 ID，返回验证 token（5分钟有效）
        /// </summary>
        public ulong BeginTransfer(ulong ownerToken, int targetThreadId)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);

            // 校验权限 token
            if (!_ownerTokens.TryGetValue(ownerToken, out var ownerInfo) || ownerInfo.Instance != this)
                throw new UnauthorizedAccessException("无效的权限 token 或 token 不属于此实例");

            if (ownerInfo.ThreadId != Environment.CurrentManagedThreadId)
                throw new UnauthorizedAccessException("权限 token 只能由绑定的线程使用");

            // TTL 校验
            if (_expireTicks != 0 && DateTime.UtcNow.Ticks > _expireTicks)
            {
                Dispose();
                throw new UnauthorizedAccessException("容器已过期，内容已被销毁。");
            }

            // 生成验证 token（5分钟有效）
            ulong verifyToken = GenerateUniqueToken(_transferTokens);
            long expireTicks = DateTime.UtcNow.AddMinutes(5).Ticks;
            _transferTokens.TryAdd(verifyToken, (this, targetThreadId, ownerToken, expireTicks));

            return verifyToken;
        }

        /// <summary>
        /// 完成转移：目标线程传入验证 token，返回新权限 token
        /// </summary>
        public ulong CompleteTransfer(ulong verifyToken)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);

            // 校验验证 token
            if (!_transferTokens.TryRemove(verifyToken, out var transferInfo))
                throw new UnauthorizedAccessException("无效的验证 token 或已过期");

            var (instance, targetThreadId, originalToken, expireTicks) = transferInfo;

            if (instance != this)
                throw new UnauthorizedAccessException("验证 token 不属于此实例");

            if (DateTime.UtcNow.Ticks > expireTicks)
                throw new UnauthorizedAccessException("验证 token 已过期");

            if (Environment.CurrentManagedThreadId != targetThreadId)
                throw new UnauthorizedAccessException("只有目标线程可以完成转移");

            // 生成新权限 token
            ulong newOwnerToken = GenerateUniqueToken(_ownerTokens);

            // 移除旧权限 token
            _ownerTokens.TryRemove(originalToken, out _);

            // 移除该实例所有访客 token（安全考虑）
            var keysToRemove = _guestTokens.Where(kvp => kvp.Value.Instance == this).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove)
                _guestTokens.TryRemove(key, out _);

            // 更新所有者线程
            _ownerThreadId = targetThreadId;
            _currentOwnerToken = newOwnerToken;

            // 注册新权限 token
            _ownerTokens.TryAdd(newOwnerToken, (this, targetThreadId));

            return newOwnerToken;
        }

        /// <summary>
        /// 借用：所有者生成访客 token，指定可用次数（访客线程首次使用时绑定）
        /// </summary>
        public ulong Borrow(ulong ownerToken, int allowedUses)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);

            if (allowedUses <= 0)
                throw new ArgumentException("允许的使用次数必须大于 0", nameof(allowedUses));

            // 校验权限 token
            if (!_ownerTokens.TryGetValue(ownerToken, out var ownerInfo) || ownerInfo.Instance != this)
                throw new UnauthorizedAccessException("无效的权限 token 或 token 不属于此实例");

            if (ownerInfo.ThreadId != Environment.CurrentManagedThreadId)
                throw new UnauthorizedAccessException("权限 token 只能由绑定的线程使用");

            // TTL 校验
            if (_expireTicks != 0 && DateTime.UtcNow.Ticks > _expireTicks)
            {
                Dispose();
                throw new UnauthorizedAccessException("容器已过期，内容已被销毁。");
            }

            // 生成访客 token（首次使用时绑定线程）
            ulong guestToken = GenerateUniqueToken(_guestTokens);
            // 使用当前线程 ID 作为初始绑定（实际在 Read 时验证）
            _guestTokens.TryAdd(guestToken, (this, Environment.CurrentManagedThreadId, allowedUses));

            return guestToken;
        }

        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            // 从全局表移除实例
            _instances.TryRemove(_instanceId, out _);

            // 清除所有相关 token
            // 1. 移除权限 token
            if (_currentOwnerToken != 0)
                _ownerTokens.TryRemove(_currentOwnerToken, out _);

            // 2. 移除所有该实例的访客 token
            var guestKeys = _guestTokens.Where(kvp => kvp.Value.Instance == this).Select(kvp => kvp.Key).ToList();
            foreach (var key in guestKeys)
                _guestTokens.TryRemove(key, out _);

            // 3. 移除所有该实例的验证 token
            var transferKeys = _transferTokens.Where(kvp => kvp.Value.Instance == this).Select(kvp => kvp.Key).ToList();
            foreach (var key in transferKeys)
                _transferTokens.TryRemove(key, out _);

            // 释放锁
            _lock?.Dispose();

            // 清零释放内存（保留原逻辑）
            if (key != null)
            {
                CryptographicOperations.ZeroMemory(new Span<byte>(key, 32));
                NativeMemory.Free(key);
                key = null;
            }
            if (data != null)
            {
                CryptographicOperations.ZeroMemory(new Span<byte>(data, length));
                NativeMemory.Free(data);
                data = null;
            }
            Array.Clear(_tag);
            Array.Clear(_nonce);
            GC.SuppressFinalize(this);
        }

        ~SEMSV() => Dispose();
    }
}
