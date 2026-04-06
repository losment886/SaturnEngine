using SaturnEngine.Global;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Shaderc;
using System.Numerics;
using System.Runtime.InteropServices;
using SaturnEngine.Management;
using Hexa.NET.SDL3;

namespace SaturnEngine.SEGraphics
{
    public unsafe class SEVulkanRender : Render
    {


        public SEVulkanRender(SEWindow h)
        :base(h,"VulkanRender","Vulkan渲染器")
        {
            
        }
        VkInstance _instance;
        Vk v;
        

        public override bool CreateDevice(int index = 0)
        {
            var windowSDL = (Hoster as SEWindowSDL).window;
            
            return true;
        }

        public override void DestroyDevice()
        {
            //throw new NotImplementedException();
        }

        public override string[] GetDeviceNames()
        {
            //var v = Vk.GetApi();
            //v.EnumeratePhysicalDevices(out uint deviceCount, null);
            //List<string> dn = new List<string>();
            //throw new Exception();
            return [];
        }

        public override void PrepareFrame(double deltaTime)
        {
            //GVariables.ThisGame.UIScene.Controls.Flush(GVariables.MainWindow.Size);
        }
        public override void Initialize()
        {
            
        }

        public override void RenderFrame(double deltaTime)
        {

        }

        public override void SetPosition(int x, int y)
        {

        }

        public override void SetScene(int index)
        {

        }

        public override void SetSize(int width, int height)
        {

        }

        public override bool CheckSupport(Feature f)
        {
            //throw new NotImplementedException();
            return false;
        }

        public override void SetFeature(Feature f, bool enable)
        {
            //throw new NotImplementedException();
        }

        public override void SetUIScene(bool enable)
        {
            //throw new NotImplementedException();
        }
   
        public override void Close()
        {
            
        }

    
    }
}
