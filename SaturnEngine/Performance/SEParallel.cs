using SaturnEngine.Base;
using SaturnEngine.Management;
using Silk.NET.OpenCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaturnEngine.Performance
{
   
    public unsafe class SEParallel : SEBase
    {
        public enum ExecutionHost
        {
            None,
            CPU = 1,
            GPU = 2,
            FPGA = 3,
            Auto = 4,
        }
        public enum MemoryType : ulong
        {
            None = 0,
            ReadWrite = 0x1,
            WriteOnly = 0x2,
            ReadOnly = 0x4,       
            UseHostPtr = 0x8,
            AllocHostPtr = 0x10,
            CopyHostPtr = 0x20,
            HostWriteOnly = 0x80,
            HostReadOnly = 0x100,
            HostNoAccess = 0x200,
            SvmFineGrainBuffer = 0x400,
            SvmAtomics = 0x800,
            KernelReadAndWrite = 0x1000,
            ExtHostPtrQCom = 0x20000000,
            UseUncachedCpuMemoryImg = 0x4000000,
            UseCachedCpuMemoryImg = 0x8000000,
            UseGrallocPtrImg = 0x10000000,
            NoAccessIntel = 0x1000000,
            AccessFlagsUnrestrictedIntel = 0x2000000,
            ForceHostMemoryIntel = 0x100000,
            ProtectedAllocArm = 0x1000000000,
        }
        CL cl;
        public ExecutionHost Host = ExecutionHost.Auto;
        nint Context;
        nint CommandQueue;
        public SEParallel() 
        {
            cl = CL.GetApi();
        }
        public void RunProgram(nint program,KeyValuePair<uint,nint>[] memObjects, uint[] globalWorkSize, uint[]? localWorkSize,string name = "main" )
        {
            int err = 0;
            nint kernel = cl.CreateKernel(program, name, out err);
            if (err != 0 || kernel == nint.Zero)
            {
                throw new Exception("创建OpenCL内核失败，错误代码：" + err);
            }
            foreach (var memObject in memObjects) 
            {
                nint v = memObject.Value;
                err = cl.SetKernelArg(kernel, memObject.Key, (nuint)nint.Size, ref v);
                if (err != 0)
                {
                    throw new Exception("设置OpenCL内核参数失败，错误代码：" + err);
                }
            }
            err = cl.EnqueueNdrangeKernel(CommandQueue, kernel, (uint)globalWorkSize.Length,((nuint*)nuint.Zero), (nuint*)(void*)&globalWorkSize, (nuint*)(void*)((localWorkSize==null)? null : &localWorkSize) , 0, null, null);
            if (err != 0) {
                throw new Exception("执行OpenCL内核失败，错误代码：" + err);
            }
            //err = cl.EnqueueNDRangeKernel(CommandQueue, kernel, 1, null, new nuint[] { (nuint)workSize }, null, 0, null, null);
            cl.ReleaseKernel(kernel);
        }
        public void ReadMemory<T>(nint memObject,ref T[] resource,uint size) where T : unmanaged
        {
            //uint size = (uint)(sizeof(T) * resource.Length);
            fixed(T* ptr = resource)
            {
                int err = cl.EnqueueReadBuffer(CommandQueue, memObject, true, 0, size, ptr, 0, null, null);
                if (err != 0)
                {
                    throw new Exception("读取OpenCL内存对象失败，错误代码：" + err);
                }
            }
        }
        public void Finish()
        {
            cl.Finish(CommandQueue);
        }
        public void ClearMemory(nint memObject)
        {
            //not implemented
            cl.ReleaseMemObject(memObject);
        }
        public void ClearProgram(nint program)
        {
            cl.ReleaseProgram(program);
        }

        public void CleanUp()
        {
            if (CommandQueue != nint.Zero)
            {
                cl.ReleaseCommandQueue(CommandQueue);
                CommandQueue = nint.Zero;
            }
            if (Context != nint.Zero)
            {
                cl.ReleaseContext(Context);
                Context = nint.Zero;
            }
            
        }
        public nint CreateMemory(uint size, MemoryType mt = MemoryType.ReadWrite)
        {
            nint handle = cl.CreateBuffer(Context, (MemFlags)mt, size, null, out int err);
            if (err == 0)
            {
                return handle;
            }
            else
            {
                throw new Exception("创建OpenCL内存对象失败，错误代码：" + err);
            }
        }
        public nint CreateMemoryFromResource<T>(ref T[] resource, MemoryType mt = MemoryType.ReadWrite | MemoryType.CopyHostPtr) where T : unmanaged
        {
            fixed(T*ptr = resource)
            {
                uint size = (uint)(sizeof(T) * resource.Length);
                nint handle = cl.CreateBuffer(Context, (MemFlags)mt, size, ptr, out int err);
                if (err == 0)
                {
                    return handle;
                }
                else
                {
                    throw new Exception("创建OpenCL内存对象失败，错误代码：" + err);
                }
            }
        }
        public nint CreateProgram(string source)
        {
            int err = 0;
            nint program = cl.CreateProgramWithSource(Context, 1, new[] { source }, null, out err);
            if (err != 0 || program == nint.Zero)
            {
                throw new Exception("创建OpenCL程序失败，错误代码：" + err);
            }
            err = cl.BuildProgram(program, 0, null, (byte*)null, null, null);
            if (err != 0)
            {
                //get build log
                nuint logSize;
                cl.GetProgramBuildInfo(program, nint.Zero, ProgramBuildInfo.BuildLog, 0, null, out logSize);
                byte[] log = new byte[logSize];
                fixed (byte* logPtr = log)
                {
                    cl.GetProgramBuildInfo(program, nint.Zero, ProgramBuildInfo.BuildLog, logSize, logPtr, out _);
                }
                string logStr = Encoding.UTF8.GetString(log);
                throw new Exception("编译OpenCL程序失败，错误代码：" + err + "，编译日志：" + logStr);
            }
            return program;
        }
        public void Create()
        {
            int err = 0;
            err = cl.GetPlatformIDs(1, out nint platforms, out uint platformCount);
            if (err != 0 || platformCount <= 0)
            {
                throw new Exception("获取OpenCL平台失败，错误代码：" + err);
            }
            nint devices;
            uint deviceCount;
            if (Host == ExecutionHost.Auto)
            {
                err = cl.GetDeviceIDs(platforms, DeviceType.Gpu, 1, out devices, out deviceCount);
                if (err != 0 && deviceCount <= 0)
                {
                    //try cpu
                    err = cl.GetDeviceIDs(platforms, DeviceType.Cpu, 1, out devices, out deviceCount);
                    if (err != 0 && deviceCount <= 0)
                    {
                        throw new Exception("自动获取OpenCL设备失败，错误代码：" + err);

                    }
                    SELogger.Log("创建为CPU设备");
                }
                else
                { 
                   
                    
                    SELogger.Log("创建为GPU设备");
                }
            }
            else if (Host == ExecutionHost.CPU)
            {
                err = cl.GetDeviceIDs(platforms, DeviceType.Cpu, 1, out devices, out deviceCount);
                if (err != 0 && deviceCount <= 0)
                {
                    throw new Exception("获取OpenCL CPU设备失败，错误代码：" + err);
                }
                SELogger.Log("创建为CPU设备");
            }
            else if (Host == ExecutionHost.GPU)
            {
                err = cl.GetDeviceIDs(platforms, DeviceType.Gpu, 1, out devices, out deviceCount);
                if (err != 0 && deviceCount <= 0)
                {
                    throw new Exception("获取OpenCL GPU设备失败，错误代码：" + err);
                }
                SELogger.Log("创建为GPU设备");
            }
            else if (Host == ExecutionHost.FPGA)
            {
                err = cl.GetDeviceIDs(platforms, DeviceType.Accelerator, 1, out devices, out deviceCount);
                if (err != 0 && deviceCount <= 0)
                {
                    throw new Exception("获取OpenCL FPGA设备失败，错误代码：" + err);
                }
                SELogger.Log("创建为FPGA设备");
            }
            else
            {
                throw new Exception("不支持的执行主机类型：" + Host.ToString());

            }

            nint context = cl.CreateContext(null, 1, ref devices, null, null, out err);
            if (err != 0 || context == nint.Zero)
            {
                throw new Exception("创建OpenCL上下文失败，错误代码：" + err);
            }
            Context = context;

            var commandQueue = cl.CreateCommandQueue(context, devices, CommandQueueProperties.None, out err);
            if (err != 0 || commandQueue == nint.Zero)
            {
                throw new Exception("创建OpenCL命令队列失败，错误代码：" + err);
            }
            CommandQueue = commandQueue;
        }
    }
}
