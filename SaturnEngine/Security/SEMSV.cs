using SaturnEngine.Performance;
using SixLabors.Fonts.Tables.AdvancedTypographic;
using System;
using System.Collections.Generic;
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
            mainT = Dispatcher.CreateThreadORG(Worker, ThreadPriority.Normal);
        }
        internal static void Close()
        {
            inited = false;
        }

        static void Worker()
        {

        }


        private byte* data;
        private int length;
        private byte* key;

        private readonly int _ownerThreadId;
        private readonly long _expireTicks;
        private readonly byte[] _nonce = new byte[12];
        private readonly byte[] _tag = new byte[16];

        internal delegate void ReadOnlySpanAction<T>(Span<T> span);
        public SEMSV(Span<byte> plaintext, TimeSpan? ttl = null)
        {
            _ownerThreadId = Environment.CurrentManagedThreadId;
            _expireTicks = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value).Ticks : 0;

            key = (byte*)NativeMemory.Alloc(32);
            RandomNumberGenerator.Fill(new Span<byte>(key, 32));
            RandomNumberGenerator.Fill(_nonce);

            length = plaintext.Length;
            data = (byte*)NativeMemory.Alloc((nuint)length);

            using var gcm = new AesGcm(new ReadOnlySpan<byte>(key, 32), 16);
            gcm.Encrypt(_nonce, plaintext, new Span<byte>(data, length), _tag);

            // 立即销毁调用方的明文副本
            CryptographicOperations.ZeroMemory(plaintext);
        }
        public void Use(ReadOnlySpanAction<byte> action)
        {
            CheckAccess();

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
        private void CheckAccess()
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);

            if (Environment.CurrentManagedThreadId != _ownerThreadId)
                throw new UnauthorizedAccessException("只有创建该容器的线程可以访问其内容。");

            if (_expireTicks != 0 && DateTime.UtcNow.Ticks > _expireTicks)
            {
                Dispose();
                throw new UnauthorizedAccessException("容器已过期，内容已被销毁。");
            }
        }
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

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
