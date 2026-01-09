using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Security;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace SaturnEngine.Management.SEMemory
{
    /// <summary>
    /// 公用长久的内存分配器
    /// </summary>
    public class GlobalMemory : SEBase, IDisposable
    {
        [Obsolete("Please use SEMemoryStream", true)]
        public unsafe class SEMemoryBlock<T>
        {
            internal nint _ptr;
            internal MemoryStream _ms;
            internal MemoryMappedFile _mmf;
            internal MemoryMappedViewStream _mmvs;
            internal bool _ped;
            internal bool _usmmf;
            internal long _size;//the total size of this block allocated,if using Ptr ,it must be small than 2GB, otherwise it will be a MemoryStream

            internal bool _llt;
            internal SEMemoryBlock(long co, bool longlt)
            {
                //Dictionary<string, object> _data = new Dictionary<string, object>();
                _size = co * Marshal.SizeOf<T>();
                _llt = longlt;
                _ped = false;
                _usmmf = true;
                if (_size < int.MaxValue)
                {
                    _ped = true;
                    _ptr = Marshal.AllocHGlobal((int)_size);
                }
                else
                {
                    //_ms = new MemoryStream();
                    try
                    {
                        _mmf = MemoryMappedFile.CreateNew(Guid.NewGuid().ToString(), _size, MemoryMappedFileAccess.ReadWrite);
                        _mmvs = _mmf.CreateViewStream();
                    }
                    catch
                    {
                        _usmmf = false;
                        _ms = new MemoryStream();
                    }

                }
            }
            public void CopyTo(ulong indexofT, ulong countofT, T* p, ulong pindex = 0)
            {
                int st = Marshal.SizeOf<T>();
                //T t;
                if (_ped)
                {
                    void* pptr = _ptr.ToPointer();
                    void* dd = Marshal.AllocHGlobal(st).ToPointer();
                    ulong ppcp = pindex;
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i += (ulong)st)
                    {
                        for (int h = 0; h < st; h++)
                        {
                            ((byte*)dd)[h] = ((byte*)pptr)[i + (ulong)h];
                        }
                        p[ppcp++] = *(T*)dd;
                    }
                }
                else if (_usmmf)
                {
                    _mmvs.Seek((long)(indexofT * (ulong)st), SeekOrigin.Begin);
                    void* dd = Marshal.AllocHGlobal(st).ToPointer();
                    ulong ppcp = pindex;
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i += (ulong)st)
                    {
                        for (int h = 0; h < st; h++)
                        {
                            ((byte*)dd)[h] = (byte)_mmvs.ReadByte();
                        }
                        p[ppcp++] = *(T*)dd;
                    }
                }
                else
                {
                    _ms.Seek((long)(indexofT * (ulong)st), SeekOrigin.Begin);
                    void* dd = Marshal.AllocHGlobal(st).ToPointer();
                    ulong ppcp = pindex;
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i += (ulong)st)
                    {
                        for (int h = 0; h < st; h++)
                        {
                            ((byte*)dd)[h] = (byte)_ms.ReadByte();
                        }
                        p[ppcp++] = *(T*)dd;
                    }
                }
            }
            public void CopyTo(ulong indexofT, ulong countofT, byte* p, ulong pindex = 0)
            {
                int st = Marshal.SizeOf<T>();
                //T t;
                if (_ped)
                {
                    void* pptr = _ptr.ToPointer();
                    void* dd = Marshal.AllocHGlobal(st).ToPointer();
                    ulong ppcp = pindex;
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i += (ulong)st)
                    {
                        for (int h = 0; h < st; h++)
                        {
                            p[ppcp++] = ((byte*)pptr)[i + (ulong)h];
                        }
                    }
                    Marshal.FreeHGlobal(new nint(dd));
                }
                else if (_usmmf)
                {
                    _mmvs.Seek((long)(indexofT * (ulong)st), SeekOrigin.Begin);
                    void* dd = Marshal.AllocHGlobal(st).ToPointer();
                    ulong ppcp = pindex;
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i += (ulong)st)
                    {
                        for (int h = 0; h < st; h++)
                        {
                            p[ppcp++] = (byte)_mmvs.ReadByte();
                        }
                    }
                    Marshal.FreeHGlobal(new nint(dd));
                }
                else
                {
                    _ms.Seek((long)(indexofT * (ulong)st), SeekOrigin.Begin);
                    void* dd = Marshal.AllocHGlobal(st).ToPointer();
                    ulong ppcp = pindex;
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i += (ulong)st)
                    {
                        for (int h = 0; h < st; h++)
                        {
                            p[ppcp++] = (byte)_ms.ReadByte();
                        }
                    }
                    Marshal.FreeHGlobal(new nint(dd));
                }
            }
            public void CopyIn(ulong indexofT, ulong countofT, T* p, ulong pindex)
            {
                int st = Marshal.SizeOf<T>();
                T t;
                if (_ped)
                {
                    ulong ppcp = pindex;
                    byte* pt;
                    //ulong bas = indexofT * (ulong)st;
                    byte* ppc = (byte*)_ptr.ToPointer();
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i += (ulong)st)
                    {
                        t = p[ppcp++];
                        pt = (byte*)(void*)&t;
                        for (int h = 0; h < st; h++)
                        {
                            ppc[i + (ulong)h] = pt[h];
                        }
                    }
                }
                else if (_usmmf)
                {
                    _mmvs.Seek((long)(indexofT * (ulong)st), SeekOrigin.Begin);
                    ulong ppcp = pindex;
                    byte* pt;
                    //ulong bas = indexofT * (ulong)st;
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i += (ulong)st)
                    {
                        t = p[ppcp++];
                        pt = (byte*)(void*)&t;
                        for (int h = 0; h < st; h++)
                        {
                            _mmvs.WriteByte(pt[h]);
                        }
                    }
                }
                else
                {
                    _ms.Seek((long)(indexofT * (ulong)st), SeekOrigin.Begin);
                    ulong ppcp = pindex;
                    byte* pt;
                    //ulong bas = indexofT * (ulong)st;
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i += (ulong)st)
                    {
                        t = p[ppcp++];
                        pt = (byte*)(void*)&t;
                        for (int h = 0; h < st; h++)
                        {
                            _ms.WriteByte(pt[h]);
                        }
                    }
                }
            }
            public void CopyIn(ulong indexofT, ulong countofT, byte* p, ulong pindex)
            {
                int st = Marshal.SizeOf<T>();
                if (_ped)
                {
                    ulong ppcp = pindex;
                    //ulong bas = indexofT * (ulong)st;
                    byte* ppc = (byte*)_ptr.ToPointer();
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i++)
                    {
                        ppc[i] = p[ppcp++];
                    }
                }
                else if (_usmmf)
                {
                    _mmvs.Seek((long)(indexofT * (ulong)st), SeekOrigin.Begin);
                    ulong ppcp = pindex;
                    //ulong bas = indexofT * (ulong)st;
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i++)
                    {
                        _mmvs.WriteByte(p[ppcp++]);
                    }
                }
                else
                {
                    _ms.Seek((long)(indexofT * (ulong)st), SeekOrigin.Begin);
                    ulong ppcp = pindex;
                    //ulong bas = indexofT * (ulong)st;
                    for (ulong i = indexofT * (ulong)st; i < (countofT + indexofT) * (ulong)st; i += (ulong)st)
                    {
                        _ms.WriteByte(p[ppcp++]);
                    }
                }
            }
            public T this[long i]
            {
                get
                {
                    if (_ped)
                    {
                        int st = Marshal.SizeOf<T>();
                        if (st == 1)
                        {
                            if (typeof(T) == typeof(byte))
                            {
                                return (T)(object)Marshal.ReadByte(_ptr, (int)i);
                            }
                            else if (typeof(T) == typeof(char))
                            {
                                return (T)(object)(char)Marshal.ReadByte(_ptr, (int)i);
                            }
                            else if (typeof(T) == typeof(bool))
                            {
                                return (T)(object)(Marshal.ReadByte(_ptr, (int)i) > 0);
                            }
                        }
                        else
                        {
                            //ptr
                            //_ptr.ToPointer()[i]
                            void* p = _ptr.ToPointer();
                            //byte[] b = new byte[Marshal.SizeOf<T>()];

                            long bad = (i) * st;
                            void* dd = Marshal.AllocHGlobal(st).ToPointer();
                            for (int h = 0; h < st; h++)
                            {
                                ((byte*)dd)[i] = ((byte*)p)[bad + i];
                            }
                            T* ppl = (T*)dd;
                            Marshal.FreeHGlobal(new nint(dd));
                            return *ppl;
                        }
                    }
                    else if (_usmmf)
                    {
                        int st = Marshal.SizeOf<T>();
                        _mmvs.Seek(i * st, SeekOrigin.Begin);
                        //mmf
                        if (st == 1)
                        {
                            if (typeof(T) == typeof(byte))
                            {
                                return (T)(object)(byte)_mmvs.ReadByte();
                            }
                            else if (typeof(T) == typeof(char))
                            {
                                return (T)(object)(char)(byte)_mmvs.ReadByte();
                            }
                            else if (typeof(T) == typeof(bool))
                            {
                                return (T)(object)((byte)_mmvs.ReadByte() > 0);
                            }
                        }
                        else
                        {
                            long bad = (i) * st;
                            void* dd = Marshal.AllocHGlobal(st).ToPointer();
                            for (int h = 0; h < st; h++)
                            {
                                ((byte*)dd)[i] = (byte)_mmvs.ReadByte();
                            }
                            T* ppl = (T*)dd;
                            Marshal.FreeHGlobal(new nint(dd));
                            return *ppl;
                        }

                    }
                    else
                    {
                        //ms
                        int st = Marshal.SizeOf<T>();
                        _ms.Seek(i * st, SeekOrigin.Begin);
                        //mmf
                        if (st == 1)
                        {
                            if (typeof(T) == typeof(byte))
                            {
                                return (T)(object)(byte)_ms.ReadByte();
                            }
                            else if (typeof(T) == typeof(char))
                            {
                                return (T)(object)(char)(byte)_ms.ReadByte();
                            }
                            else if (typeof(T) == typeof(bool))
                            {
                                return (T)(object)((byte)_ms.ReadByte() > 0);
                            }
                        }
                        else
                        {
                            long bad = (i) * st;
                            void* dd = Marshal.AllocHGlobal(st).ToPointer();
                            for (int h = 0; h < st; h++)
                            {
                                ((byte*)dd)[i] = (byte)_ms.ReadByte();
                            }
                            T* ppl = (T*)dd;
                            Marshal.FreeHGlobal(new nint(dd));
                            return *ppl;
                        }
                    }
                    //return null;
                    return *((T*)nint.Zero.ToPointer());
                }
                set
                {
                    T v = value;
                    int st = Marshal.SizeOf<T>();
                    if (_ped)
                    {
                        if (st == 1)
                        {
                            if (typeof(T) == typeof(byte))
                            {
                                //return (T)(object)Marshal.ReadByte(_ptr, (int)i);
                                ((byte*)_ptr.ToPointer())[i * st] = (byte)(object)v;
                            }
                            else if (typeof(T) == typeof(char))
                            {
                                //return (T)(object)(char)Marshal.ReadByte(_ptr, (int)i);
                                ((char*)_ptr.ToPointer())[i * st] = (char)(object)v;
                            }
                            else if (typeof(T) == typeof(bool))
                            {
                                //return (T)(object)(Marshal.ReadByte(_ptr, (int)i) > 0);
                                ((bool*)_ptr.ToPointer())[i * st] = (bool)(object)v;
                            }
                        }
                        else
                        {
                            byte* p = (byte*)(void*)&v;
                            long bad = (i) * st;
                            for (int l = 0; l < st; l++)
                            {
                                ((byte*)_ptr.ToPointer())[bad + l] = p[l];
                            }
                        }
                    }
                    else if (_usmmf)
                    {
                        if (st == 1)
                        {
                            if (typeof(T) == typeof(byte))
                            {
                                _mmvs.Seek(i * st, SeekOrigin.Begin);
                                //return (T)(object)Marshal.ReadByte(_ptr, (int)i);
                                _mmvs.WriteByte((byte)(object)v);
                            }
                            else if (typeof(T) == typeof(char))
                            {
                                //return (T)(object)(char)Marshal.ReadByte(_ptr, (int)i);
                                //*((char*)_ptr.ToPointer()) = (char)(object)v;
                                _mmvs.Seek(i * st, SeekOrigin.Begin);
                                //return (T)(object)Marshal.ReadByte(_ptr, (int)i);
                                _mmvs.WriteByte((byte)(object)v);
                            }
                            else if (typeof(T) == typeof(bool))
                            {
                                //return (T)(object)(Marshal.ReadByte(_ptr, (int)i) > 0);
                                //*((bool*)_ptr.ToPointer()) = (bool)(object)v;
                                _mmvs.Seek(i * st, SeekOrigin.Begin);
                                //return (T)(object)Marshal.ReadByte(_ptr, (int)i);
                                _mmvs.WriteByte((bool)(object)v ? (byte)1 : (byte)0);
                            }
                        }
                        else
                        {
                            byte* p = (byte*)(void*)&v;
                            long bad = (i) * st;
                            _mmvs.Seek(bad, SeekOrigin.Begin);
                            for (int l = 0; l < st; l++)
                            {
                                _mmvs.WriteByte(p[l]);
                            }
                        }
                    }
                    else
                    {
                        if (st == 1)
                        {
                            if (typeof(T) == typeof(byte))
                            {
                                _ms.Seek(i * st, SeekOrigin.Begin);
                                //return (T)(object)Marshal.ReadByte(_ptr, (int)i);
                                _ms.WriteByte((byte)(object)v);
                            }
                            else if (typeof(T) == typeof(char))
                            {
                                //return (T)(object)(char)Marshal.ReadByte(_ptr, (int)i);
                                //*((char*)_ptr.ToPointer()) = (char)(object)v;
                                _ms.Seek(i * st, SeekOrigin.Begin);
                                //return (T)(object)Marshal.ReadByte(_ptr, (int)i);
                                _ms.WriteByte((byte)(object)v);
                            }
                            else if (typeof(T) == typeof(bool))
                            {
                                //return (T)(object)(Marshal.ReadByte(_ptr, (int)i) > 0);
                                //*((bool*)_ptr.ToPointer()) = (bool)(object)v;
                                _ms.Seek(i * st, SeekOrigin.Begin);
                                //return (T)(object)Marshal.ReadByte(_ptr, (int)i);
                                _ms.WriteByte((bool)(object)v ? (byte)1 : (byte)0);
                            }
                        }
                        else
                        {
                            byte* p = (byte*)(void*)&v;
                            long bad = (i) * st;
                            _ms.Seek(bad, SeekOrigin.Begin);
                            for (int l = 0; l < st; l++)
                            {
                                _ms.WriteByte(p[l]);
                            }
                        }
                    }
                }
            }
        }

        //SEMemoryBlock<dynamic>[] blocks;
        //Dictionary<ulong, SEMemoryBlock<dynamic>> blocks;
        List<SEMemoryStream> blocks;
        List<ulong> stcs;
        public ulong TotalUsed = 0;
        public GlobalMemory()
        {
            //blocks = new Dictionary<ulong, SEMemoryBlock<dynamic>>(0);
            blocks = new List<SEMemoryStream>(0);
            stcs = new List<ulong>(0);
        }
        public void FreeMemory(ulong nmstc)
        {
            int co = stcs.IndexOf(nmstc);
            stcs.RemoveAt(co);
            var ms = blocks[co];
            blocks.RemoveAt(co);

            TotalUsed -= (ulong)ms.MemoryUsage;
            ms.Dispose();

        }
        public void AllocateMemory(long length, string name)
        {
            SEMemoryStream sp = new SEMemoryStream(length);
            var stc = STCCode.GetSTC(name);
            blocks.Add(sp);
            stcs.Add(stc);
            TotalUsed += (ulong)sp.MemoryUsage;
        }
        public SEMemoryStream GetMemoryBlock(ulong stc)
        {
            int index = stcs.IndexOf(stc);
            if (index >= 0 && index < blocks.Count)
            {
                return blocks[index];
            }
            else
            {
                throw new Exception("没有对应STC码的内存块".GetInCurrLang());
            }
        }
        public SEMemoryStream GetMemoryBlock(string nm)
        {
            int index = stcs.IndexOf(STCCode.GetSTC(nm));
            if (index >= 0 && index < blocks.Count)
            {
                return blocks[index];
            }
            else
            {
                throw new Exception("没有对应STC码的内存块".GetInCurrLang());
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                blocks[i].Dispose();
            }
        }
    }



}
