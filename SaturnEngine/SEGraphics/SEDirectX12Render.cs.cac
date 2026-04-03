using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using System.Runtime.InteropServices;

namespace SaturnEngine.SEGraphics
{
    public unsafe class SEDirectX12Render : Render
    {
        ComPtr<IDXGIFactory6> dxgifactory;
        DXGI dg;
        D3D12 d12;

        public override bool CheckSupport(Feature f)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            //throw new NotImplementedException();
        }

        public override bool CreateDevice(int index = 0)
        {
            //throw new NotImplementedException();
            return true;
        }

        public override void DestroyDevice()
        {
            //throw new NotImplementedException();
        }

        public override string[] GetDeviceNames()
        {
            //throw new NotImplementedException();
            List<string> dn = new List<string>();
            ComPtr<IDXGIAdapter1> ada;
            AdapterDesc1 desc;
            for (uint i = 0; dxgifactory.EnumAdapterByGpuPreference(i, GpuPreference.HighPerformance, out ada) >= 0; i++)
            {
                if (ada.GetDesc1(&desc) >= 0)
                {
                    dn.Add($"D{i} VendorID:{desc.VendorId} Description:{Marshal.PtrToStringUTF8(new nint(desc.Description))}");
                }
            }
            return dn.ToArray();
        }

        public override void Initialize()
        {
            //throw new NotImplementedException();
            dg = DXGI.GetApi();
            d12 = D3D12.GetApi();
            if (dg.CreateDXGIFactory2(0u, out dxgifactory) < 0)
                throw new Exception("Failed to create DXGI Factory");

        }

        public override void PrepareFrame(double deltaTime)
        {

        }

        public override void RenderFrame(double deltaTime)
        {
            //throw new NotImplementedException();
        }

        public override void SetFeature(Feature f, bool enable)
        {
            //throw new NotImplementedException();
        }

        public override void SetPosition(int x, int y)
        {
            //throw new NotImplementedException();
        }

        public override void SetScene(int index)
        {
            //throw new NotImplementedException();
        }

        public override void SetSize(int width, int height)
        {
            //throw new NotImplementedException();
        }

        public override void SetUIScene(bool enable)
        {
            //throw new NotImplementedException();
        }
    }
}
