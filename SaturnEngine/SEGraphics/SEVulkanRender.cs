using SaturnEngine.Global;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using SaturnEngine.Management;

namespace SaturnEngine.SEGraphics
{
    public unsafe class SEVulkanRender : Render
    {
        Instance _instance;
        Vk v;

        public override void Close()
        {

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
            //throw new NotImplementedException();
            v = Vk.GetApi();
            uint ve = 0;
            if (v.EnumerateInstanceVersion(ref ve) == Result.Success)
            {
                //Console.WriteLine(ve+" | "+ Vk.Version13.V2);
                ApplicationInfo ai = new ApplicationInfo();
                ai.ApiVersion = ve;
                ai.ApplicationVersion = new Version32(1, 0, 0);
                ai.EngineVersion = new Version32((uint)GVariables.EngineVersion.Major, (uint)GVariables.EngineVersion.Minor, (uint)GVariables.EngineVersion.Build);
                string EN = "SaturnEngine";
                string AN = GVariables.ProgramName ?? "SE";
                ai.PApplicationName = (byte*)&AN;
                ai.PEngineName = (byte*)&EN;
                ai.SType = StructureType.ApplicationInfo;
                InstanceCreateInfo ici = new InstanceCreateInfo();
                ici.PApplicationInfo = &ai;
                ici.SType = StructureType.InstanceCreateInfo;

                uint ec = 0;
                byte** ext;
                ext = Silk.NET.GLFW.Glfw.GetApi().GetRequiredInstanceExtensions(out ec);

                ici.EnabledExtensionCount = ec;
                ici.PpEnabledExtensionNames = ext;
                ici.EnabledLayerCount = 0;

                if (v.CreateInstance(&ici, null, out _instance) == Result.Success)
                {
                    SELogger.Log("Vulkan Instance created.");
                }
                else
                {
                    throw new Exception("Failed to create Vk Instance");
                }
            }
            else
            {
                throw new Exception("Failed to get Vulkan version.");
            }
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
    }
}
