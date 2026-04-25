using SaturnEngine.Global;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Shaderc;
using System.Numerics;
using System.Runtime.InteropServices;
using SaturnEngine.Asset;
using SaturnEngine.Management;
using Silk.NET.SDL;
using System.Text;
using Avalonia.Controls.Platform;
using SaturnEngine.Management.SEMemory;
using Silk.NET.Core.Native;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace SaturnEngine.SEGraphics
{
    public unsafe class SEVulkanRender : Render
    {


        public SEVulkanRender(SEWindow h)
        : base(h, "VulkanRender", "Vulkan渲染器")
        {
            v = Vk.GetApi();
            Hoster = h;

            // Initialize all internal variables
            khrSurface = new SEStaticPtr<KhrSurface>();
            khrSwapchain = new SEStaticPtr<KhrSwapchain>();
            physicalDevice = new SEStaticPtr<PhysicalDevice>();
            device = new SEStaticPtr<Device>();
            graphicsQueue = new SEStaticPtr<Queue>();
            presentQueue = new SEStaticPtr<Queue>();
            surface = new SEStaticPtr<SurfaceKHR>();
            swapChain = new SEStaticPtr<SwapchainKHR>();
            swapChainImages = new SEStaticPtr<Image>(new Image[0]);
            swapChainImageViews = new SEStaticPtr<ImageView>(new ImageView[0]);
            swapChainImageFormat = new SEStaticPtr<Format>();
            swapChainExtent = new SEStaticPtr<Extent2D>();
            renderPass = new SEStaticPtr<RenderPass>();
            pipelineLayout = new SEStaticPtr<PipelineLayout>();
            graphicsPipeline = new SEStaticPtr<Pipeline>();
            swapChainFramebuffers = new SEStaticPtr<Framebuffer>(new Framebuffer[0]);
            commandPool = new SEStaticPtr<CommandPool>();
            commandBuffers = new SEStaticPtr<CommandBuffer>(new CommandBuffer[MAX_FRAMES_IN_FLIGHT]);
            imageAvailableSemaphores = new SEStaticPtr<Semaphore>(new Semaphore[MAX_FRAMES_IN_FLIGHT]);
            renderFinishedSemaphores = new SEStaticPtr<Semaphore>(new Semaphore[MAX_FRAMES_IN_FLIGHT]);
            inFlightFences = new SEStaticPtr<Fence>(new Fence[MAX_FRAMES_IN_FLIGHT]);
            vertexBuffer = new SEStaticPtr<Buffer>();
            vertexBufferMemory = new SEStaticPtr<DeviceMemory>();
            indexBuffer = new SEStaticPtr<Buffer>();
            indexBufferMemory = new SEStaticPtr<DeviceMemory>();
            uniformBuffers = new SEStaticPtr<Buffer>(new Buffer[MAX_FRAMES_IN_FLIGHT]);
            uniformBuffersMemory = new SEStaticPtr<DeviceMemory>(new DeviceMemory[MAX_FRAMES_IN_FLIGHT]);
            descriptorSetLayout = new SEStaticPtr<DescriptorSetLayout>();
            descriptorPool = new SEStaticPtr<DescriptorPool>();
            descriptorSets = new SEStaticPtr<DescriptorSet>(new DescriptorSet[MAX_FRAMES_IN_FLIGHT]);
            vertShaderModule = new SEStaticPtr<ShaderModule>();
            fragShaderModule = new SEStaticPtr<ShaderModule>();

            // Query required Vulkan instance extensions via SDL.
            if (Hoster is SEWindowSDL)
            {
                byte** extname = null;
            uint cou = 0;
            var sdl = Sdl.GetApi();

            // First call with pNames = null to get required count
            if (sdl.VulkanGetInstanceExtensions((Window*)Hoster.GetWindowHandle().ToPointer(), ref cou, (byte**)null) == SdlBool.True && cou > 0)
            {
                // Allocate native array for pointers (byte*)
                IntPtr namesPtr = Marshal.AllocHGlobal((int)cou * IntPtr.Size);
                extname = (byte**)namesPtr.ToPointer();

                if (sdl.VulkanGetInstanceExtensions((Window*)Hoster.GetWindowHandle().ToPointer(), ref cou, extname) != SdlBool.True)
                {
                    SELogger.Error("Failed to retrieve Vulkan instance extension names from SDL", "SEVulkanRender");
                    Marshal.FreeHGlobal(namesPtr);
                    extname = null;
                    cou = 0;
                }
            }
            SELogger.Log($"SDL reports {cou} required Vulkan instance extensions", "SEVulkanRender");
            //byte[] b = [..extensions.SelectMany(s => Encoding.ASCII.GetBytes(s + "\0")).ToArray()];
            ApplicationInfo ai = new ApplicationInfo()
            {
                ApiVersion = new Version32(1, 3, 0),
                ApplicationVersion = new Version32(0, 1, 0),
                EngineVersion = new Version32(0, 1, 0),
                PApplicationName = (byte*)Marshal.StringToHGlobalAnsi(GVariables.ProgramName).ToPointer(),
                PEngineName = (byte*)Marshal.StringToHGlobalAnsi("SaturnEngine").ToPointer(),
                SType = StructureType.ApplicationInfo,
            };

            InstanceCreateInfo ici = new InstanceCreateInfo()
            {
                PApplicationInfo = &ai,
                Flags = InstanceCreateFlags.None,
                SType = StructureType.InstanceCreateInfo,
                EnabledExtensionCount = cou,
                PpEnabledExtensionNames = extname,
                PNext = null,
                EnabledLayerCount = 0,
                PpEnabledLayerNames = null
            };
            Instance ins = new Instance();
            Result r;
            if ((r = v.CreateInstance(ref ici, null, &ins)) == Result.Success)
            {
                instance = ins;
                GetDeviceNames().ToList().ForEach(x => SELogger.Log($"检测到Vulkan设备: {x}", "SEVulkanRender"));
            }
            else
            {
                SELogger.Error($"无法创建Vulkan实例 : {Marshal.GetLastSystemError()} : {Marshal.GetLastPInvokeErrorMessage()} : {r.ToString()}".GetInCurrLang(), "SEVulkanRender");
            }

            // Free the temporary array of pointers (strings returned by SDL are owned by SDL; only the pointer array was allocated)
            if (extname != null)
            {
                Marshal.FreeHGlobal((IntPtr)extname);
            }
            }
            else
            {
                var w = Hoster as SEWindowSilk;
                //w.window.First.VkSurface
                alr = true;
                ApplicationInfo ai = new ApplicationInfo()
                {
                    ApiVersion = new Version32(1, 3, 0),
                    ApplicationVersion = new Version32(0, 1, 0),
                    EngineVersion = new Version32(0, 1, 0),
                    PApplicationName = (byte*)Marshal.StringToHGlobalAnsi(GVariables.ProgramName).ToPointer(),
                    PEngineName = (byte*)Marshal.StringToHGlobalAnsi("SaturnEngine").ToPointer(),
                    SType = StructureType.ApplicationInfo,
                };

                InstanceCreateInfo ici = new InstanceCreateInfo()
                {
                    PApplicationInfo = &ai,
                    Flags = InstanceCreateFlags.None,
                    SType = StructureType.InstanceCreateInfo,
                    EnabledExtensionCount = 0,
                    PpEnabledExtensionNames = null,
                    PNext = null,
                    EnabledLayerCount = 0,
                    PpEnabledLayerNames = null
                };
                Instance ins = new Instance();
                Result r;
                if ((r = v.CreateInstance(ref ici, null, &ins)) == Result.Success)
                {
                    instance = ins;
                    GetDeviceNames().ToList().ForEach(x => SELogger.Log($"检测到Vulkan设备: {x}", "SEVulkanRender"));
                }
                else
                {
                    SELogger.Error($"无法创建Vulkan实例 : {Marshal.GetLastSystemError()} : {Marshal.GetLastPInvokeErrorMessage()} : {r.ToString()}".GetInCurrLang(), "SEVulkanRender");
                }
                //khrSurface = new SEStaticPtr<KhrSurface>(new KhrSurface(w.window.First.VkSurface.));
            }
        }

        private bool alr = false;
        Instance instance;
        Vk v;
        private SEStaticPtr<KhrSurface> khrSurface;
        private SEStaticPtr<KhrSwapchain> khrSwapchain;
        
        private SEStaticPtr<PhysicalDevice> physicalDevice;
        private SEStaticPtr<Device> device;
        private SEStaticPtr<Queue> graphicsQueue;
        private SEStaticPtr<Queue> presentQueue;
        private SEStaticPtr<SurfaceKHR> surface;
        private SEStaticPtr<SwapchainKHR> swapChain;
        private SEStaticPtr<Image> swapChainImages;
        private SEStaticPtr<ImageView> swapChainImageViews;
        private SEStaticPtr<Format> swapChainImageFormat;
        private SEStaticPtr<Extent2D> swapChainExtent;
        private SEStaticPtr<RenderPass> renderPass;
        private SEStaticPtr<PipelineLayout> pipelineLayout;
        private SEStaticPtr<Pipeline> graphicsPipeline;
        private SEStaticPtr<Framebuffer> swapChainFramebuffers;
        private SEStaticPtr<CommandPool> commandPool;
        private SEStaticPtr<CommandBuffer> commandBuffers;
        private SEStaticPtr<Semaphore> imageAvailableSemaphores;
        private SEStaticPtr<Semaphore> renderFinishedSemaphores;
        private SEStaticPtr<Fence> inFlightFences;
        private int currentFrame = 0;
        private const int MAX_FRAMES_IN_FLIGHT = 2;
        private bool framebufferResized = false;

        // Vertex and index buffers for cube
        private SEStaticPtr<Buffer> vertexBuffer;
        private SEStaticPtr<DeviceMemory> vertexBufferMemory;
        private SEStaticPtr<Buffer> indexBuffer;
        private SEStaticPtr<DeviceMemory> indexBufferMemory;
        private uint indicesCount;

        // Uniform buffers
        private SEStaticPtr<Buffer> uniformBuffers;
        private SEStaticPtr<DeviceMemory> uniformBuffersMemory;
        private SEStaticPtr<DescriptorSetLayout> descriptorSetLayout;
        private SEStaticPtr<DescriptorPool> descriptorPool;
        private SEStaticPtr<DescriptorSet> descriptorSets;

        // Shader modules
        private SEStaticPtr<ShaderModule> vertShaderModule;
        private SEStaticPtr<ShaderModule> fragShaderModule;

        public unsafe override bool CreateDevice(int index = 0)
        {
            //var windowSDL = (Hoster as SEWindowSDL).window;
            PhysicalDevice pd = v.GetPhysicalDevices(instance).ElementAt(index);
            float[] f = new float[]{0.0f};
            uint qfc = 0;
            QueueFamilyProperties qfp = new QueueFamilyProperties();
            v.GetPhysicalDeviceQueueFamilyProperties(pd, ref qfc, &qfp);
            
            
            DeviceQueueCreateInfo dqci = new DeviceQueueCreateInfo()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                Flags = 0,
                PNext = null,
                QueueFamilyIndex = 0,
                QueueCount = 1,
                PQueuePriorities = (float*)&f,
            };
            DeviceCreateInfo dci = new DeviceCreateInfo()
            {
                SType = StructureType.DeviceCreateInfo,
                Flags = 0,
                PNext = null,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &dqci,
                EnabledLayerCount = 0,
                PpEnabledLayerNames = null,
                EnabledExtensionCount = 0,
                PpEnabledExtensionNames = null,
                PEnabledFeatures = null
            };
            device = new SEStaticPtr<Device>();
            if (v.CreateDevice(pd, &dci, null, device.Handle) == Result.Success)
            {
                return true;
            }
            SELogger.Error("创建vk设备失败".GetInCurrLang(),"SEVulkanRender");
            return false;
        }

        public override void DestroyDevice()
        {
            //throw new NotImplementedException();
        }

        public override string[] GetDeviceNames()
        {
            //var v = Vk.GetApi();
            uint deviceCount = 0;
            v.EnumeratePhysicalDevices(instance, ref deviceCount, null);
            List<string> dn = new List<string>();
            foreach (var item in v.GetPhysicalDevices(instance))
            {
                PhysicalDeviceProperties pdp;
                v.GetPhysicalDeviceProperties(item, &pdp);
                dn.Add(Marshal.PtrToStringAnsi((IntPtr)pdp.DeviceName)??"Unknown Device");
            }
            //throw new Exception();
            return dn.ToArray();
        }

        public override void PrepareFrame(double deltaTime)
        {
            //GVariables.ThisGame.UIScene.Controls.Flush(GVariables.MainWindow.Size);
        }
        public override void Initialize()
        {
            CreateSurface();
            PickPhysicalDevice();
            CreateLogicalDevice();
            CreateSwapChain();
            CreateImageViews();
            CreateRenderPass();
            CreateDescriptorSetLayout();
            CreateGraphicsPipeline();
            CreateFramebuffers();
            CreateCommandPool();
            CreateVertexBuffer();
            CreateIndexBuffer();
            CreateUniformBuffers();
            CreateDescriptorPool();
            CreateDescriptorSets();
            CreateCommandBuffers();
            CreateSyncObjects();
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
        

        private unsafe void CreateSurface()
        {
            if (alr)
            {
                var w = Hoster as SEWindowSilk;
                w.window.First.Initialize();
                khrSurface.Handle = (KhrSurface*)w.window.First.VkSurface.Create<int>(new VkHandle(instance.Handle), null).Handle;
            }
            else
            {
                var windowSDL = Hoster as SEWindowSDL;
                if (windowSDL == null) return;

                // Get the KHR surface extension
                if (!v.TryGetInstanceExtension(instance, out *khrSurface.Handle))
                {
                    SELogger.Error("Failed to get KHR surface extension", "SEVulkanRender");
                    return;
                }

                // Create surface using SDL
                SurfaceKHR surf;
                VkNonDispatchableHandle vndh = new VkNonDispatchableHandle();
                if (Sdl.GetApi().VulkanCreateSurface(windowSDL.window, instance.ToHandle(), &vndh) == SdlBool.True)
                {
                    surface = new SEStaticPtr<SurfaceKHR>(new SurfaceKHR(vndh.Handle));//Sdl.GetApi().GetWindowSurface(windowSDL.window)
                    SELogger.Log("Vulkan surface created successfully", "SEVulkanRender");
                }
                else
                {
                    SELogger.Error($"Failed to create Vulkan surface : SDL: {Marshal.PtrToStringUTF8(new nint(Sdl.GetApi().GetError()))} : ", "SEVulkanRender");
                }
            }
        }

        private void PickPhysicalDevice()
        {
            var devices = v.GetPhysicalDevices(instance);
            foreach (var device in devices)
            {
                if (IsDeviceSuitable(device))
                {
                    physicalDevice = new SEStaticPtr<PhysicalDevice>(device);
                    break;
                }
            }
            
            if (physicalDevice.Handle->Handle == 0)
            {
                SELogger.Error("Failed to find a suitable GPU", "SEVulkanRender");
            }
        }

        private bool IsDeviceSuitable(PhysicalDevice device)
        {
            // Check for graphics queue family
            uint queueFamilyCount = 0;
            v.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);
            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            {
                v.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);
            }

            uint graphicsFamily = uint.MaxValue;
            uint presentFamily = uint.MaxValue;
            for (uint i = 0; i < queueFamilyCount; i++)
            {
                if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
                {
                    graphicsFamily = i;
                }
                Bool32 presentSupport = false;
                khrSurface.Handle->GetPhysicalDeviceSurfaceSupport(device
                    , i, surface.First, &presentSupport);
                if (presentSupport)
                {
                    presentFamily = i;
                }
            }

            return graphicsFamily != uint.MaxValue && presentFamily != uint.MaxValue;
        }

        private void CreateLogicalDevice()
        {
            uint queueFamilyCount = 0;
            v.GetPhysicalDeviceQueueFamilyProperties(physicalDevice.First, ref queueFamilyCount, null);
            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            {
                v.GetPhysicalDeviceQueueFamilyProperties(physicalDevice.First, ref queueFamilyCount, queueFamiliesPtr);
            }

            uint graphicsFamily = uint.MaxValue;
            uint presentFamily = uint.MaxValue;
            for (uint i = 0; i < queueFamilyCount; i++)
            {
                if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
                {
                    graphicsFamily = i;
                }
                Bool32 presentSupport = false;
                khrSurface.Handle->GetPhysicalDeviceSurfaceSupport(physicalDevice.First, i, surface.First, &presentSupport);
                if (presentSupport)
                {
                    presentFamily = i;
                }
            }

            var uniqueFamilies = new HashSet<uint> { graphicsFamily, presentFamily };
            var queueCreateInfos = new DeviceQueueCreateInfo[uniqueFamilies.Count];
            float queuePriority = 1.0f;
            int index = 0;
            foreach (var family in uniqueFamilies)
            {
                queueCreateInfos[index] = new DeviceQueueCreateInfo
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = family,
                    QueueCount = 1,
                    PQueuePriorities = &queuePriority
                };
                index++;
            }

            PhysicalDeviceFeatures deviceFeatures = new PhysicalDeviceFeatures();
            //char* deviceExtensions = stackalloc char[KhrSwapchain.ExtensionName.ToCharArray().Length];
            //Marshal.Copy(KhrSwapchain.ExtensionName.ToCharArray(), 0, (IntPtr)deviceExtensions, KhrSwapchain.ExtensionName.Length);
            byte* de = (byte*)Marshal.StringToHGlobalAnsi(KhrSwapchain.ExtensionName).ToPointer();

            fixed (DeviceQueueCreateInfo* queueCreateInfosPtr = queueCreateInfos)
            {
                DeviceCreateInfo createInfo = new DeviceCreateInfo
                {
                    SType = StructureType.DeviceCreateInfo,
                    PQueueCreateInfos = queueCreateInfosPtr,
                    QueueCreateInfoCount = (uint)queueCreateInfos.Length,
                    PEnabledFeatures = &deviceFeatures,
                    EnabledExtensionCount = (uint)1,
                    PpEnabledExtensionNames = &de
                };
                Result r;
                if ((r = v.CreateDevice(physicalDevice.First, &createInfo, null, out *device.Handle)) != Result.Success)
                {
                    SELogger.Error($"Failed to create logical device: {r}", "SEVulkanRender");

                }
            }

            v.GetDeviceQueue(device.First, graphicsFamily, 0, out *graphicsQueue.Handle);
            v.GetDeviceQueue(device.First, presentFamily, 0, out *presentQueue.Handle);

            // Get swapchain extension
            if (!v.TryGetDeviceExtension(instance, device.First, out *khrSwapchain.Handle))
            {
                SELogger.Error("Failed to get KHR swapchain extension", "SEVulkanRender");
            }
        }

        private void CreateSwapChain()
        {
            var swapChainSupport = QuerySwapChainSupport(physicalDevice.First);

            var surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
            var presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);
            var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

            uint imageCount = swapChainSupport.Capabilities.MinImageCount + 1;
            if (swapChainSupport.Capabilities.MaxImageCount > 0 && imageCount > swapChainSupport.Capabilities.MaxImageCount)
            {
                imageCount = swapChainSupport.Capabilities.MaxImageCount;
            }

            SwapchainCreateInfoKHR createInfo = new SwapchainCreateInfoKHR
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = surface.First,
                MinImageCount = imageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = extent,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ColorAttachmentBit,
                PreTransform = swapChainSupport.Capabilities.CurrentTransform,
                CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
                PresentMode = presentMode,
                Clipped = true,
                OldSwapchain = default
            };

            uint queueFamilyIndices = FindQueueFamilies(physicalDevice.First).GraphicsFamily.Value;
            createInfo.ImageSharingMode = SharingMode.Exclusive;
            createInfo.QueueFamilyIndexCount = 0;
            createInfo.PQueueFamilyIndices = null;

            if (khrSwapchain.Handle->CreateSwapchain(device.First
                    , &createInfo, null, out *swapChain.Handle) != Result.Success) 
            {
                SELogger.Error("Failed to create swap chain", "SEVulkanRender");
            }

            khrSwapchain.Handle->GetSwapchainImages(device.First, swapChain.First, ref imageCount, null);
            swapChainImages = new SEStaticPtr<Image>(new Image[imageCount]);
            khrSwapchain.Handle->GetSwapchainImages(device.First, swapChain.First, ref imageCount, (Image*)(swapChainImages.Handle));

            swapChainImageFormat[0] = surfaceFormat.Format;
            swapChainExtent[0] = extent;
        }

        private SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device)
        {
            SwapChainSupportDetails details = new SwapChainSupportDetails();

            khrSurface.Handle->GetPhysicalDeviceSurfaceCapabilities(device, surface.First, out details.Capabilities);

            uint formatCount = 0;
            khrSurface.Handle->GetPhysicalDeviceSurfaceFormats(device, surface.First, ref formatCount, null);
            if (formatCount != 0)
            {
                details.Formats = new SurfaceFormatKHR[formatCount];
                fixed (SurfaceFormatKHR* formatsPtr = details.Formats)
                {
                    khrSurface.Handle->GetPhysicalDeviceSurfaceFormats(device, surface.First, ref formatCount, formatsPtr);
                }
            }

            uint presentModeCount = 0;
            khrSurface.Handle->GetPhysicalDeviceSurfacePresentModes(device, surface.First, ref presentModeCount, null);
            if (presentModeCount != 0)
            {
                details.PresentModes = new PresentModeKHR[presentModeCount];
                fixed (PresentModeKHR* modesPtr = details.PresentModes)
                {
                    khrSurface.Handle->GetPhysicalDeviceSurfacePresentModes(device, surface.First, ref presentModeCount, modesPtr);
                }
            }

            return details;
        }

        private SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
        {
            foreach (var availableFormat in availableFormats)
            {
                if (availableFormat.Format == Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                {
                    return availableFormat;
                }
            }
            return availableFormats[0];
        }

        private PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
        {
            foreach (var availablePresentMode in availablePresentModes)
            {
                if (availablePresentMode == PresentModeKHR.MailboxKhr)
                {
                    return availablePresentMode;
                }
            }
            return PresentModeKHR.FifoKhr;
        }

        private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.CurrentExtent.Width != uint.MaxValue)
            {
                return capabilities.CurrentExtent;
            }
            else
            {
                var actualExtent = new Extent2D
                {
                    Width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, (uint)Hoster.Size.X)),
                    Height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, (uint)Hoster.Size.Y))
                };
                return actualExtent;
            }
        }

        private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
        {
            QueueFamilyIndices indices = new QueueFamilyIndices();

            uint queueFamilyCount = 0;
            v.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);
            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            {
                v.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);
            }

            for (uint i = 0; i < queueFamilyCount; i++)
            {
                if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
                {
                    indices.GraphicsFamily = i;
                }

                Bool32 presentSupport = false;
                khrSurface.Handle->GetPhysicalDeviceSurfaceSupport(device, i, surface.First, &presentSupport);
                if (presentSupport)
                {
                    indices.PresentFamily = i;
                }

                if (indices.IsComplete())
                {
                    break;
                }
            }

            return indices;
        }

        private void CreateImageViews()
        {
            swapChainImageViews = new SEStaticPtr<ImageView>(new ImageView[swapChainImages.Length]);

            for (int i = 0; i < swapChainImages.Length; i++)
            {
                ImageViewCreateInfo createInfo = new ImageViewCreateInfo
                {
                    SType = StructureType.ImageViewCreateInfo,
                    Image = swapChainImages.Handle[i],
                    ViewType = ImageViewType.Type2D,
                    Format = swapChainImageFormat.First,
                    Components = new ComponentMapping
                    {
                        R = ComponentSwizzle.Identity,
                        G = ComponentSwizzle.Identity,
                        B = ComponentSwizzle.Identity,
                        A = ComponentSwizzle.Identity
                    },
                    SubresourceRange = new ImageSubresourceRange
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        BaseMipLevel = 0,
                        LevelCount = 1,
                        BaseArrayLayer = 0,
                        LayerCount = 1
                    }
                };

                if (v.CreateImageView(device.First, &createInfo, null, out swapChainImageViews.Handle[i]) != Result.Success)
                {
                    SELogger.Error("Failed to create image views", "SEVulkanRender");
                }
            }
        }

        private void CreateRenderPass()
        {
            AttachmentDescription colorAttachment = new AttachmentDescription
            {
                Format = swapChainImageFormat.First,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.PresentSrcKhr
            };

            AttachmentReference colorAttachmentRef = new AttachmentReference
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal
            };

            SubpassDescription subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef
            };

            SubpassDependency dependency = new SubpassDependency
            {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                SrcAccessMask = 0,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                DstAccessMask = AccessFlags.ColorAttachmentWriteBit
            };

            RenderPassCreateInfo renderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = &colorAttachment,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = 1,
                PDependencies = &dependency
            };

            if (v.CreateRenderPass(device.First, &renderPassInfo, null, out *renderPass.Handle) != Result.Success)
            {
                SELogger.Error("Failed to create render pass", "SEVulkanRender");
            }
        }

        private void CreateDescriptorSetLayout()
        {
            DescriptorSetLayoutBinding uboLayoutBinding = new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit,
                PImmutableSamplers = null
            };

            DescriptorSetLayoutCreateInfo layoutInfo = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = 1,
                PBindings = &uboLayoutBinding
            };

            if (v.CreateDescriptorSetLayout(device.First, &layoutInfo, null, out *descriptorSetLayout.Handle) != Result.Success)
            {
                SELogger.Error("Failed to create descriptor set layout", "SEVulkanRender");
            }
        }

        private void CreateGraphicsPipeline()
        {
            var vertShaderCode = LoadShader("vert.glsl");
            var fragShaderCode = LoadShader("frag.glsl");

            vertShaderModule = new SEStaticPtr<ShaderModule>(CreateShaderModule(vertShaderCode));
            fragShaderModule = new SEStaticPtr<ShaderModule>(CreateShaderModule(fragShaderCode));

            PipelineShaderStageCreateInfo vertShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertShaderModule.First,
                PName = (byte*)Marshal.StringToHGlobalAnsi("main").ToPointer()
            };

            PipelineShaderStageCreateInfo fragShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragShaderModule.First,
                PName = (byte*)Marshal.StringToHGlobalAnsi("main").ToPointer()
            };

            PipelineShaderStageCreateInfo[] shaderStages = { vertShaderStageInfo, fragShaderStageInfo };

            VertexInputBindingDescription bindingDescription = new VertexInputBindingDescription
            {
                Binding = 0,
                Stride = (uint)sizeof(Vertex),
                InputRate = VertexInputRate.Vertex
            };

            VertexInputAttributeDescription[] attributeDescriptions = new VertexInputAttributeDescription[2];
            attributeDescriptions[0] = new VertexInputAttributeDescription
            {
                Binding = 0,
                Location = 0,
                Format = Format.R32G32B32Sfloat,
                Offset = 0
            };
            attributeDescriptions[1] = new VertexInputAttributeDescription
            {
                Binding = 0,
                Location = 1,
                Format = Format.R32G32B32Sfloat,
                Offset = (uint)sizeof(Vector3)
            };

            PipelineVertexInputStateCreateInfo vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = 2,
                PVertexAttributeDescriptions = (VertexInputAttributeDescription*)&attributeDescriptions
            };

            PipelineInputAssemblyStateCreateInfo inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = false
            };

            Viewport viewport = new Viewport
            {
                X = 0.0f,
                Y = 0.0f,
                Width = swapChainExtent.First.Width,
                Height = swapChainExtent.First.Height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            };

            Rect2D scissor = new Rect2D
            {
                Offset = new Offset2D { X = 0, Y = 0 },
                Extent = swapChainExtent.First
            };

            PipelineViewportStateCreateInfo viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                PViewports = &viewport,
                ScissorCount = 1,
                PScissors = &scissor
            };

            PipelineRasterizationStateCreateInfo rasterizer = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.BackBit,
                FrontFace = FrontFace.CounterClockwise,
                DepthBiasEnable = false
            };

            PipelineMultisampleStateCreateInfo multisampling = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = false,
                RasterizationSamples = SampleCountFlags.Count1Bit
            };

            PipelineColorBlendAttachmentState colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                BlendEnable = false
            };

            PipelineColorBlendStateCreateInfo colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment
            };
            //var temp1 = descriptorSetLayout;
            PipelineLayoutCreateInfo pipelineLayoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = descriptorSetLayout.Handle,
                PushConstantRangeCount = 0
            };

            if (v.CreatePipelineLayout(device.First, &pipelineLayoutInfo, null, out *pipelineLayout.Handle) != Result.Success)
            {
                SELogger.Error("Failed to create pipeline layout", "SEVulkanRender");
            }

            GraphicsPipelineCreateInfo pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = (PipelineShaderStageCreateInfo*)&shaderStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PColorBlendState = &colorBlending,
                Layout = pipelineLayout.First,
                RenderPass = renderPass.First,
                Subpass = 0,
                BasePipelineHandle = default,
                BasePipelineIndex = -1
            };
            PipelineCache ppc = new PipelineCache();
            
            Result r;
            if ((r = v.CreateGraphicsPipelines(device.First, ppc, 1, &pipelineInfo, null, graphicsPipeline.Handle)) != Result.Success)
            {
                SELogger.Error($"Failed to create graphics pipeline: {r}", "SEVulkanRender");
            }

            v.DestroyShaderModule(device.First, fragShaderModule.First, null);
            v.DestroyShaderModule(device.First, vertShaderModule.First, null);
        }

        private byte[] LoadShader(string filename)
        {
            // For simplicity, embed the shader code here
            if (filename == "vert.glsl")
            {
                return Encoding.UTF8.GetBytes(@"
#version 450
layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inColor;

layout(binding = 0) uniform UniformBufferObject {
    mat4 model;
    mat4 view;
    mat4 proj;
} ubo;

layout(location = 0) out vec3 fragColor;

void main() {
    gl_Position = ubo.proj * ubo.view * ubo.model * vec4(inPosition, 1.0);
    fragColor = inColor;
}
");
            }
            else if (filename == "frag.glsl")
            {
                return Encoding.UTF8.GetBytes(@"
#version 450
layout(location = 0) in vec3 fragColor;

layout(location = 0) out vec4 outColor;

void main() {
    outColor = vec4(fragColor, 1.0);
}
");
            }
            return new byte[0];
        }

        private ShaderModule CreateShaderModule(byte[] code)
        {
            ShaderModuleCreateInfo createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
                PCode = (uint*)Marshal.UnsafeAddrOfPinnedArrayElement(code, 0)
            };

            if (v.CreateShaderModule(device.First, &createInfo, null, out ShaderModule shaderModule) != Result.Success)
            {
                SELogger.Error("Failed to create shader module", "SEVulkanRender");
            }

            return shaderModule;
        }

        private void CreateFramebuffers()
        {
            swapChainFramebuffers = new SEStaticPtr<Framebuffer>(new Framebuffer[swapChainImageViews.Length]);

            for (int i = 0; i < swapChainImageViews.Length; i++)
            {
                ImageView* attachments = stackalloc ImageView[1] { swapChainImageViews.Handle[i] };

                FramebufferCreateInfo framebufferInfo = new FramebufferCreateInfo
                {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = renderPass.First,
                    AttachmentCount = 1,
                    PAttachments = attachments,
                    Width = swapChainExtent.First.Width,
                    Height = swapChainExtent.First.Height,
                    Layers = 1
                };

                if (v.CreateFramebuffer(device.First, &framebufferInfo, null, out swapChainFramebuffers.Handle[i]) != Result.Success)
                {
                    SELogger.Error("Failed to create framebuffer", "SEVulkanRender");
                }
            }
        }

        private void CreateCommandPool()
        {
            QueueFamilyIndices queueFamilyIndices = FindQueueFamilies(physicalDevice.First);

            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = queueFamilyIndices.GraphicsFamily.Value
            };

            if (v.CreateCommandPool(device.First, &poolInfo, null, out *commandPool.Handle) != Result.Success)
            {
                SELogger.Error("Failed to create command pool", "SEVulkanRender");
            }
        }

        private void CreateVertexBuffer()
        {
            Vertex[] vertices = {
                new Vertex { Pos = new Vector3(-0.5f, -0.5f, 0.5f), Color = new Vector3(1.0f, 0.0f, 0.0f) },
                new Vertex { Pos = new Vector3(0.5f, -0.5f, 0.5f), Color = new Vector3(0.0f, 1.0f, 0.0f) },
                new Vertex { Pos = new Vector3(0.5f, 0.5f, 0.5f), Color = new Vector3(0.0f, 0.0f, 1.0f) },
                new Vertex { Pos = new Vector3(-0.5f, 0.5f, 0.5f), Color = new Vector3(1.0f, 1.0f, 1.0f) },
                new Vertex { Pos = new Vector3(-0.5f, -0.5f, -0.5f), Color = new Vector3(1.0f, 0.0f, 0.0f) },
                new Vertex { Pos = new Vector3(0.5f, -0.5f, -0.5f), Color = new Vector3(0.0f, 1.0f, 0.0f) },
                new Vertex { Pos = new Vector3(0.5f, 0.5f, -0.5f), Color = new Vector3(0.0f, 0.0f, 1.0f) },
                new Vertex { Pos = new Vector3(-0.5f, 0.5f, -0.5f), Color = new Vector3(1.0f, 1.0f, 1.0f) }
            };
            float[] vt =
            {
                -0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
                0.5f, -0.5f, 0.5f, 0.0f, 1.0f, 0.0f,
                0.5f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f,
                -0.5f, 0.5f, 0.5f, 1.0f, 1.0f, 1.0f,
                -0.5f, -0.5f, -0.5f, 1.0f, 0.0f, 0.0f,
                0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
                0.5f, 0.5f, -0.5f, 0.0f, 0.0f, 1.0f,
                -0.5f, 0.5f, -0.5f, 1.0f, 1.0f, 1.0f
            };

            ulong bufferSize = (ulong)(sizeof(Vertex) * vertices.Length);

            Buffer stagingBuffer;
            DeviceMemory stagingBufferMemory;
            CreateBuffer(bufferSize, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out stagingBuffer, out stagingBufferMemory);

            void* data;
            v.MapMemory(device.First, stagingBufferMemory, 0, bufferSize, 0, &data);
            Marshal.Copy(vt, 0, (nint)data, vt.Length);
            
            v.UnmapMemory(device.First, stagingBufferMemory);

            Buffer tempVertexBuffer;
            DeviceMemory tempVertexBufferMemory;
            CreateBuffer(bufferSize, BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.DeviceLocalBit, out tempVertexBuffer, out tempVertexBufferMemory);
            vertexBuffer = new SEStaticPtr<Buffer>(tempVertexBuffer);
            vertexBufferMemory = new SEStaticPtr<DeviceMemory>(tempVertexBufferMemory);

            CopyBuffer(stagingBuffer, tempVertexBuffer, bufferSize);

            v.DestroyBuffer(device.First, stagingBuffer, null);
            v.FreeMemory(device.First, stagingBufferMemory, null);
        }

        private void CreateIndexBuffer()
        {
            short[] indices = {
                0, 1, 2, 2, 3, 0,
                1, 5, 6, 6, 2, 1,
                7, 6, 5, 5, 4, 7,
                4, 0, 3, 3, 7, 4,
                4, 5, 1, 1, 0, 4,
                3, 2, 6, 6, 7, 3
            };

            indicesCount = (uint)indices.Length;
            ulong bufferSize = (ulong)(sizeof(ushort) * indices.Length);

            Buffer stagingBuffer;
            DeviceMemory stagingBufferMemory;
            CreateBuffer(bufferSize, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out stagingBuffer, out stagingBufferMemory);

            void* data;
            v.MapMemory(device.First, stagingBufferMemory, 0, bufferSize, 0, &data);
            Marshal.Copy(indices, 0, (IntPtr)data, indices.Length);
            v.UnmapMemory(device.First, stagingBufferMemory);

            Buffer tempIndexBuffer;
            DeviceMemory tempIndexBufferMemory;
            CreateBuffer(bufferSize, BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit, MemoryPropertyFlags.DeviceLocalBit, out tempIndexBuffer, out tempIndexBufferMemory);
            indexBuffer = new SEStaticPtr<Buffer>(tempIndexBuffer);
            indexBufferMemory = new SEStaticPtr<DeviceMemory>(tempIndexBufferMemory);

            CopyBuffer(stagingBuffer, tempIndexBuffer, bufferSize);

            v.DestroyBuffer(device.First, stagingBuffer, null);
            v.FreeMemory(device.First, stagingBufferMemory, null);
        }

        private void CreateUniformBuffers()
        {
            ulong bufferSize = (ulong)sizeof(UniformBufferObject);

            uniformBuffers = new SEStaticPtr<Buffer>(new Buffer[MAX_FRAMES_IN_FLIGHT]);
            uniformBuffersMemory = new SEStaticPtr<DeviceMemory>(new DeviceMemory[MAX_FRAMES_IN_FLIGHT]);

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                CreateBuffer(bufferSize, BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out uniformBuffers.Handle[i], out uniformBuffersMemory.Handle[i]);
            }
        }

        private void CreateDescriptorPool()
        {
            DescriptorPoolSize poolSize = new DescriptorPoolSize
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = (uint)MAX_FRAMES_IN_FLIGHT
            };

            DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = 1,
                PPoolSizes = &poolSize,
                MaxSets = (uint)MAX_FRAMES_IN_FLIGHT
            };

            if (v.CreateDescriptorPool(device.First, &poolInfo, null, out *descriptorPool.Handle) != Result.Success)
            {
                SELogger.Error("Failed to create descriptor pool", "SEVulkanRender");
            }
        }

        private void CreateDescriptorSets()
        {
            DescriptorSetLayout[] layouts = new DescriptorSetLayout[MAX_FRAMES_IN_FLIGHT];
            Array.Fill(layouts, descriptorSetLayout.First);

            DescriptorSetAllocateInfo allocInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = descriptorPool.First,
                DescriptorSetCount = (uint)MAX_FRAMES_IN_FLIGHT,
                PSetLayouts = (DescriptorSetLayout*)&layouts
            };

            descriptorSets = new SEStaticPtr<DescriptorSet>(new DescriptorSet[MAX_FRAMES_IN_FLIGHT]);
            if (v.AllocateDescriptorSets(device.First, &allocInfo, descriptorSets.Handle) != Result.Success)
            {
                SELogger.Error("Failed to allocate descriptor sets", "SEVulkanRender");
            }

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                DescriptorBufferInfo bufferInfo = new DescriptorBufferInfo
                {
                    Buffer = uniformBuffers.Handle[i],
                    Offset = 0,
                    Range = (ulong)sizeof(UniformBufferObject)
                };

                WriteDescriptorSet descriptorWrite = new WriteDescriptorSet
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = descriptorSets.Handle[i],
                    DstBinding = 0,
                    DstArrayElement = 0,
                    DescriptorType = DescriptorType.UniformBuffer,
                    DescriptorCount = 1,
                    PBufferInfo = &bufferInfo
                };

                v.UpdateDescriptorSets(device.First, 1, &descriptorWrite, 0, null);
            }
        }

        private void CreateCommandBuffers()
        {
            commandBuffers = new SEStaticPtr<CommandBuffer>(new CommandBuffer[MAX_FRAMES_IN_FLIGHT]);

            CommandBufferAllocateInfo allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = commandPool.First,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)commandBuffers.Length
            };

            if (v.AllocateCommandBuffers(device.First, &allocInfo, commandBuffers.Handle) != Result.Success)
            {
                SELogger.Error("Failed to allocate command buffers", "SEVulkanRender");
            }
        }

        private void CreateSyncObjects()
        {
            imageAvailableSemaphores = new SEStaticPtr<Semaphore>(new Semaphore[MAX_FRAMES_IN_FLIGHT]);
            renderFinishedSemaphores = new SEStaticPtr<Semaphore>(new Semaphore[MAX_FRAMES_IN_FLIGHT]);
            inFlightFences = new SEStaticPtr<Fence>(new Fence[MAX_FRAMES_IN_FLIGHT]);

            SemaphoreCreateInfo semaphoreInfo = new SemaphoreCreateInfo
            {
                SType = StructureType.SemaphoreCreateInfo
            };

            FenceCreateInfo fenceInfo = new FenceCreateInfo
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit
            };

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                if (v.CreateSemaphore(device.First, &semaphoreInfo, null, out imageAvailableSemaphores.Handle[i]) != Result.Success ||
                    v.CreateSemaphore(device.First, &semaphoreInfo, null, out renderFinishedSemaphores.Handle[i]) != Result.Success ||
                    v.CreateFence(device.First, &fenceInfo, null, out inFlightFences.Handle[i]) != Result.Success)
                {
                    SELogger.Error("Failed to create synchronization objects for a frame", "SEVulkanRender");
                }
            }
        }

        private void CreateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, out Buffer buffer, out DeviceMemory bufferMemory)
        {
            BufferCreateInfo bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = size,
                Usage = usage,
                SharingMode = SharingMode.Exclusive
            };

            if (v.CreateBuffer(device.First, &bufferInfo, null, out buffer) != Result.Success)
            {
                SELogger.Error("Failed to create buffer", "SEVulkanRender");
            }

            MemoryRequirements memRequirements;
            v.GetBufferMemoryRequirements(device.First, buffer, out memRequirements);

            MemoryAllocateInfo allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties)
            };

            if (v.AllocateMemory(device.First, &allocInfo, null, out bufferMemory) != Result.Success)
            {
                SELogger.Error("Failed to allocate buffer memory", "SEVulkanRender");
            }

            v.BindBufferMemory(device.First, buffer, bufferMemory, 0);
        }

        private void CopyBuffer(Buffer srcBuffer, Buffer dstBuffer, ulong size)
        {
            CommandBufferAllocateInfo allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1,
                CommandPool = commandPool.First
            };

            CommandBuffer commandBuffer;
            v.AllocateCommandBuffers(device.First, &allocInfo, &commandBuffer);

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };

            v.BeginCommandBuffer(commandBuffer, &beginInfo);

            BufferCopy copyRegion = new BufferCopy
            {
                Size = size
            };

            v.CmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, &copyRegion);

            v.EndCommandBuffer(commandBuffer);

            SubmitInfo submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer
            };

            v.QueueSubmit(graphicsQueue.First, 1, &submitInfo, default);
            v.QueueWaitIdle(graphicsQueue.First);

            v.FreeCommandBuffers(device.First, commandPool.First, 1, &commandBuffer);
        }

        private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
        {
            PhysicalDeviceMemoryProperties memProperties;
            v.GetPhysicalDeviceMemoryProperties(physicalDevice.First, out memProperties);

            for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                if ((typeFilter & (1 << (int)i)) != 0 && (memProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
                {
                    return i;
                }
            }

            SELogger.Error("Failed to find suitable memory type", "SEVulkanRender");
            return 0;
        }

        public override void RenderFrame(double deltaTime)
        {
            v.WaitForFences(device.First, 1, inFlightFences.Handle[currentFrame], true, ulong.MaxValue);

            uint imageIndex;
            var result = khrSwapchain.Handle->AcquireNextImage(device.First, swapChain.First, ulong.MaxValue, imageAvailableSemaphores.Handle[currentFrame], default, &imageIndex);

            if (result == Result.ErrorOutOfDateKhr)
            {
                RecreateSwapChain();
                return;
            }
            else if (result != Result.Success && result != Result.SuboptimalKhr)
            {
                SELogger.Error("Failed to acquire swap chain image", "SEVulkanRender");
            }

            v.ResetFences(device.First, 1, inFlightFences.Handle[currentFrame]);

            v.ResetCommandBuffer(commandBuffers.Handle[currentFrame], 0);
            RecordCommandBuffer(commandBuffers.Handle[currentFrame], imageIndex);

            UpdateUniformBuffer((uint)currentFrame, deltaTime);
            var temp = stackalloc PipelineStageFlags[1] { PipelineStageFlags.ColorAttachmentOutputBit };
            SubmitInfo submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = &imageAvailableSemaphores.Handle[ currentFrame],
                PWaitDstStageMask = temp,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffers.Handle[ currentFrame],
                SignalSemaphoreCount = 1,
                PSignalSemaphores = &renderFinishedSemaphores.Handle[ currentFrame]
            };

            if (v.QueueSubmit(graphicsQueue.First, 1, &submitInfo, inFlightFences.Handle[currentFrame]) != Result.Success)
            {
                SELogger.Error("Failed to submit draw command buffer", "SEVulkanRender");
            }

            PresentInfoKHR presentInfo = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = &renderFinishedSemaphores.Handle[ currentFrame],
                SwapchainCount = 1,
                PSwapchains = swapChain.Handle,
                PImageIndices = &imageIndex
            };

            result = khrSwapchain.Handle->QueuePresent(presentQueue.First, &presentInfo);

            if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr || framebufferResized)
            {
                framebufferResized = false;
                RecreateSwapChain();
            }
            else if (result != Result.Success)
            {
                SELogger.Error("Failed to present swap chain image", "SEVulkanRender");
            }

            currentFrame = (currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
        }

        private void RecordCommandBuffer(CommandBuffer commandBuffer, uint imageIndex)
        {
            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo
            };

            if (v.BeginCommandBuffer(commandBuffer, &beginInfo) != Result.Success)
            {
                SELogger.Error("Failed to begin recording command buffer", "SEVulkanRender");
            }

            RenderPassBeginInfo renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = renderPass.First,
                Framebuffer = swapChainFramebuffers.Handle[imageIndex],
                RenderArea = new Rect2D
                {
                    Offset = new Offset2D { X = 0, Y = 0 },
                    Extent = swapChainExtent.First
                }
            };

            ClearValue clearColor = new ClearValue
            {
                Color = new ClearColorValue { Float32_0 = 0.0f, Float32_1 = 0.0f, Float32_2 = 0.0f, Float32_3 = 1.0f }
            };

            renderPassInfo.ClearValueCount = 1;
            renderPassInfo.PClearValues = &clearColor;

            v.CmdBeginRenderPass(commandBuffer, &renderPassInfo, SubpassContents.Inline);

            v.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, graphicsPipeline.First);

            ulong[] offsets = { 0 };
            v.CmdBindVertexBuffers(commandBuffer, 0, 1, vertexBuffer.Handle, offsets);
            v.CmdBindIndexBuffer(commandBuffer, indexBuffer.First, 0, IndexType.Uint16);

            v.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, pipelineLayout.First, 0, 1, &descriptorSets.Handle[currentFrame], 0, null);

            v.CmdDrawIndexed(commandBuffer, indicesCount, 1, 0, 0, 0);

            v.CmdEndRenderPass(commandBuffer);

            if (v.EndCommandBuffer(commandBuffer) != Result.Success)
            {
                SELogger.Error("Failed to record command buffer", "SEVulkanRender");
            }
        }

        private void UpdateUniformBuffer(uint currentImage, double deltaTime)
        {
            UniformBufferObject ubo = new UniformBufferObject();

            // Rotate the cube
            ubo.Model = Matrix4x4.CreateRotationY((float)deltaTime * 0.5f);
            ubo.View = Matrix4x4.CreateLookAt(new Vector3(2.0f, 2.0f, 2.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f));
            ubo.Proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4.0f, swapChainExtent.First.Width / (float)swapChainExtent.First.Height, 0.1f, 10.0f);
            ubo.Proj.M22 *= -1; // Flip Y for Vulkan

            void* data;
            v.MapMemory(device.First, uniformBuffersMemory.Handle[currentImage], 0, (ulong)sizeof(UniformBufferObject), 0, &data);
            Marshal.StructureToPtr(ubo, (IntPtr)data, false);
            v.UnmapMemory(device.First, uniformBuffersMemory.Handle[currentImage]);
        }

        private void RecreateSwapChain()
        {
            v.DeviceWaitIdle(device.First);

            CleanupSwapChain();

            CreateSwapChain();
            CreateImageViews();
            CreateRenderPass();
            CreateGraphicsPipeline();
            CreateFramebuffers();
        }

        private void CleanupSwapChain()
        {
            foreach (var framebuffer in swapChainFramebuffers)
            {
                v.DestroyFramebuffer(device.First, framebuffer, null);
            }

            v.DestroyPipeline(device.First, graphicsPipeline.First, null);
            v.DestroyPipelineLayout(device.First, pipelineLayout.First, null);
            v.DestroyRenderPass(device.First, renderPass.First, null);

            foreach (var imageView in swapChainImageViews)
            {
                v.DestroyImageView(device.First, imageView, null);
            }

            khrSwapchain.Handle->DestroySwapchain(device.First, swapChain.First, null);
        }

        public override void Close()
        {
            v.DeviceWaitIdle(device.First);

            CleanupSwapChain();

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                v.DestroySemaphore(device.First, renderFinishedSemaphores.Handle[i], null);
                v.DestroySemaphore(device.First, imageAvailableSemaphores.Handle[i], null);
                v.DestroyFence(device.First, inFlightFences.Handle[i], null);
            }

            v.DestroyCommandPool(device.First, commandPool.First, null);

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                v.DestroyBuffer(device.First, uniformBuffers.Handle[i], null);
                v.FreeMemory(device.First, uniformBuffersMemory.Handle[i], null);
            }

            v.DestroyDescriptorPool(device.First, descriptorPool.First, null);
            v.DestroyDescriptorSetLayout(device.First, descriptorSetLayout.First, null);

            v.DestroyBuffer(device.First, indexBuffer.First, null);
            v.FreeMemory(device.First, indexBufferMemory.First, null);

            v.DestroyBuffer(device.First, vertexBuffer.First, null);
            v.FreeMemory(device.First, vertexBufferMemory.First, null);

            v.DestroyDevice(device.First, null);
            khrSurface.Handle->DestroySurface(instance, surface.First, null);
            v.DestroyInstance(instance, null);
        }

        private struct Vertex
        {
            public Vector3 Pos;
            public Vector3 Color;
        }

        private struct UniformBufferObject
        {
            public Matrix4x4 Model;
            public Matrix4x4 View;
            public Matrix4x4 Proj;
        }

        private struct QueueFamilyIndices
        {
            public uint? GraphicsFamily;
            public uint? PresentFamily;

            public bool IsComplete()
            {
                return GraphicsFamily.HasValue && PresentFamily.HasValue;
            }
        }

        private struct SwapChainSupportDetails
        {
            public SurfaceCapabilitiesKHR Capabilities;
            public SurfaceFormatKHR[] Formats;
            public PresentModeKHR[] PresentModes;
        }
    
    }
}
