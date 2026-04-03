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
        private Vk vk;
        private VkInstance instance;
        private SurfaceKHR surface;
        private PhysicalDevice physicalDevice;
        private Device device;
        private Queue graphicsQueue;
        private Queue presentQueue;
        private SwapchainKHR swapChain;
        private Image[] swapChainImages;
        private ImageView[] swapChainImageViews;
        private RenderPass renderPass;
        private PipelineLayout pipelineLayout;
        private Pipeline graphicsPipeline;
        private Framebuffer[] swapChainFramebuffers;
        private CommandPool commandPool;
        private CommandBuffer[] commandBuffers;
        private Semaphore[] imageAvailableSemaphores;
        private Semaphore[] renderFinishedSemaphores;
        private Fence[] inFlightFences;
        private int currentFrame = 0;
        private const int maxFramesInFlight = 2;
        private Format swapChainImageFormat;
        private Extent2D swapChainExtent;
        private Buffer vertexBuffer;
        private DeviceMemory vertexBufferMemory;
        private Buffer indexBuffer;
        private DeviceMemory indexBufferMemory;
        private uint[] indices;
        private double time = 0;
        private Shaderc shaderc;

        public SEVulkanRender(SEWindow h)
        :base(h,"VulkanRender","Vulkan渲染器")
        {
            
        }
        VkInstance _instance;
        Vk v;
        

        public override bool CreateDevice(int index = 0)
        {
            var windowSDL = (Hoster as SEWindowSDL).window;
            VkSurfaceKHR surface = new VkSurfaceKHR();
            SDL.VulkanCreateSurface(windowSDL, instance,default(VkAllocationCallbacksPtr), ref surface);
            uint deviceCount = 0;
            vk.EnumeratePhysicalDevices(instance, ref deviceCount, null);
            if (deviceCount == 0)
            {
                throw new Exception("Failed to find GPUs with Vulkan support");
            }
            PhysicalDevice[] devices = new PhysicalDevice[deviceCount];
            vk.EnumeratePhysicalDevices(instance, ref deviceCount, devices);
            physicalDevice = devices[index];
            QueueFamilyIndices indices = FindQueueFamilies(physicalDevice);
            DeviceQueueCreateInfo queueCreateInfo = new DeviceQueueCreateInfo();
            queueCreateInfo.SType = StructureType.DeviceQueueCreateInfo;
            queueCreateInfo.QueueFamilyIndex = indices.graphicsFamily.Value;
            queueCreateInfo.QueueCount = 1;
            float queuePriority = 1.0f;
            queueCreateInfo.PQueuePriorities = &queuePriority;
            DeviceCreateInfo createInfo = new DeviceCreateInfo();
            createInfo.SType = StructureType.DeviceCreateInfo;
            createInfo.PQueueCreateInfos = &queueCreateInfo;
            createInfo.QueueCreateInfoCount = 1;
            string[] deviceExtensions = { "VK_KHR_swapchain" };
            createInfo.EnabledExtensionCount = (uint)deviceExtensions.Length;
            createInfo.PpEnabledExtensionNames = deviceExtensions;
            if (vk.CreateDevice(physicalDevice, &createInfo, null, out device) != Result.Success)
            {
                throw new Exception("Failed to create logical device");
            }
            vk.GetDeviceQueue(device, indices.graphicsFamily.Value, 0, out graphicsQueue);
            vk.GetDeviceQueue(device, indices.presentFamily.Value, 0, out presentQueue);
            CreateSwapChain();
            CreateImageViews();
            CreateRenderPass();
            CreateGraphicsPipeline();
            CreateFramebuffers();
            CreateCommandPool();
            CreateVertexBuffer();
            CreateIndexBuffer();
            CreateCommandBuffers();
            CreateSyncObjects();
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
            vk = Vk.GetApi();
            shaderc = Shaderc.GetApi();
            uint ve = 0;
            if (vk.EnumerateInstanceVersion(ref ve) == Result.Success)
            {
                ApplicationInfo ai = new ApplicationInfo();
                ai.ApiVersion = ve;
                ai.ApplicationVersion = new Version32(1, 0, 0);
                ai.EngineVersion = new Version32((uint)GVariables.EngineVersion.Major, (uint)GVariables.EngineVersion.Minor, (uint)GVariables.EngineVersion.Build);
                string EN = "SaturnEngine";
                string AN = GVariables.ProgramName ?? "SE";
                ai.PApplicationName = (byte*)&AN;
                ai.PEngineName = (byte*)&EN;
                ai.SType = StructureType.ApplicationInfo;
                string[] extensions = { "VK_KHR_surface", "VK_KHR_win32_surface" };
                InstanceCreateInfo ici = new InstanceCreateInfo();
                ici.PApplicationInfo = &ai;
                ici.SType = StructureType.InstanceCreateInfo;
                ici.EnabledExtensionCount = (uint)extensions.Length;
                ici.PpEnabledExtensionNames = extensions;
                ici.EnabledLayerCount = 0;
                if (vk.CreateInstance(&ici, null, out instance) == Result.Success)
                {
                    SELogger.Log("Vulkan Instance created.");
                    CreateDevice(0);
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

        private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
        {
            QueueFamilyIndices indices = new QueueFamilyIndices();
            uint queueFamilyCount = 0;
            vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);
            QueueFamilyProperties[] queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamilies);
            for (int i = 0; i < queueFamilyCount; i++)
            {
                if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
                {
                    indices.graphicsFamily = i;
                }
                Bool32 presentSupport = false;
                vk.GetPhysicalDeviceSurfaceSupportKHR(device, (uint)i, surface, &presentSupport);
                if (presentSupport)
                {
                    indices.presentFamily = i;
                }
                if (indices.IsComplete())
                {
                    break;
                }
            }
            return indices;
        }

        private SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device)
        {
            SwapChainSupportDetails details = new SwapChainSupportDetails();
            vk.GetPhysicalDeviceSurfaceCapabilitiesKHR(device, surface, out details.capabilities);
            uint formatCount = 0;
            vk.GetPhysicalDeviceSurfaceFormatsKHR(device, surface, ref formatCount, null);
            if (formatCount != 0)
            {
                details.formats = new SurfaceFormatKHR[formatCount];
                vk.GetPhysicalDeviceSurfaceFormatsKHR(device, surface, ref formatCount, details.formats);
            }
            uint presentModeCount = 0;
            vk.GetPhysicalDeviceSurfacePresentModesKHR(device, surface, ref presentModeCount, null);
            if (presentModeCount != 0)
            {
                details.presentModes = new PresentModeKHR[presentModeCount];
                vk.GetPhysicalDeviceSurfacePresentModesKHR(device, surface, ref presentModeCount, details.presentModes);
            }
            return details;
        }

        private SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
        {
            foreach (var availableFormat in availableFormats)
            {
                if (availableFormat.Format == Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.SrgbNonlinearKhr)
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
                Extent2D actualExtent = new Extent2D();
                actualExtent.Width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, (uint)h.Size.X));
                actualExtent.Height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, (uint)h.Size.Y));
                return actualExtent;
            }
        }

        private void CreateSwapChain()
        {
            SwapChainSupportDetails swapChainSupport = QuerySwapChainSupport(physicalDevice);
            SurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.formats);
            PresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.presentModes);
            Extent2D extent = ChooseSwapExtent(swapChainSupport.capabilities);
            uint imageCount = swapChainSupport.capabilities.MinImageCount + 1;
            if (swapChainSupport.capabilities.MaxImageCount > 0 && imageCount > swapChainSupport.capabilities.MaxImageCount)
            {
                imageCount = swapChainSupport.capabilities.MaxImageCount;
            }
            SwapchainCreateInfoKHR createInfo = new SwapchainCreateInfoKHR();
            createInfo.SType = StructureType.SwapchainCreateInfoKhr;
            createInfo.Surface = surface;
            createInfo.MinImageCount = imageCount;
            createInfo.ImageFormat = surfaceFormat.Format;
            createInfo.ImageColorSpace = surfaceFormat.ColorSpace;
            createInfo.ImageExtent = extent;
            createInfo.ImageArrayLayers = 1;
            createInfo.ImageUsage = ImageUsageFlags.ColorAttachmentBit;
            QueueFamilyIndices indices = FindQueueFamilies(physicalDevice);
            uint[] queueFamilyIndices = { indices.graphicsFamily.Value, indices.presentFamily.Value };
            if (indices.graphicsFamily != indices.presentFamily)
            {
                createInfo.ImageSharingMode = SharingMode.Concurrent;
                createInfo.QueueFamilyIndexCount = 2;
                createInfo.PQueueFamilyIndices = queueFamilyIndices;
            }
            else
            {
                createInfo.ImageSharingMode = SharingMode.Exclusive;
            }
            createInfo.PreTransform = swapChainSupport.capabilities.CurrentTransform;
            createInfo.CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr;
            createInfo.PresentMode = presentMode;
            createInfo.Clipped = true;
            if (vk.CreateSwapchainKHR(device, &createInfo, null, out swapChain) != Result.Success)
            {
                throw new Exception("Failed to create swap chain");
            }
            vk.GetSwapchainImagesKHR(device, swapChain, ref imageCount, null);
            swapChainImages = new Image[imageCount];
            vk.GetSwapchainImagesKHR(device, swapChain, ref imageCount, swapChainImages);
            swapChainImageFormat = surfaceFormat.Format;
            swapChainExtent = extent;
        }

        private void CreateImageViews()
        {
            swapChainImageViews = new ImageView[swapChainImages.Length];
            for (int i = 0; i < swapChainImages.Length; i++)
            {
                ImageViewCreateInfo createInfo = new ImageViewCreateInfo();
                createInfo.SType = StructureType.ImageViewCreateInfo;
                createInfo.Image = swapChainImages[i];
                createInfo.ViewType = ImageViewType.View2D;
                createInfo.Format = swapChainImageFormat;
                createInfo.Components.R = ComponentSwizzle.Identity;
                createInfo.Components.G = ComponentSwizzle.Identity;
                createInfo.Components.B = ComponentSwizzle.Identity;
                createInfo.Components.A = ComponentSwizzle.Identity;
                createInfo.SubresourceRange.AspectMask = ImageAspectFlags.ColorBit;
                createInfo.SubresourceRange.BaseMipLevel = 0;
                createInfo.SubresourceRange.LevelCount = 1;
                createInfo.SubresourceRange.BaseArrayLayer = 0;
                createInfo.SubresourceRange.LayerCount = 1;
                if (vk.CreateImageView(device, &createInfo, null, out swapChainImageViews[i]) != Result.Success)
                {
                    throw new Exception("Failed to create image views");
                }
            }
        }

        private void CreateRenderPass()
        {
            AttachmentDescription colorAttachment = new AttachmentDescription();
            colorAttachment.Format = swapChainImageFormat;
            colorAttachment.Samples = SampleCountFlags.Count1Bit;
            colorAttachment.LoadOp = AttachmentLoadOp.Clear;
            colorAttachment.StoreOp = AttachmentStoreOp.Store;
            colorAttachment.StencilLoadOp = AttachmentLoadOp.DontCare;
            colorAttachment.StencilStoreOp = AttachmentStoreOp.DontCare;
            colorAttachment.InitialLayout = ImageLayout.Undefined;
            colorAttachment.FinalLayout = ImageLayout.PresentSrcKhr;
            AttachmentReference colorAttachmentRef = new AttachmentReference();
            colorAttachmentRef.Attachment = 0;
            colorAttachmentRef.Layout = ImageLayout.ColorAttachmentOptimal;
            SubpassDescription subpass = new SubpassDescription();
            subpass.PipelineBindPoint = PipelineBindPoint.Graphics;
            subpass.ColorAttachmentCount = 1;
            subpass.PColorAttachments = &colorAttachmentRef;
            SubpassDependency dependency = new SubpassDependency();
            dependency.SrcSubpass = Vk.SubpassExternal;
            dependency.DstSubpass = 0;
            dependency.SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit;
            dependency.SrcAccessMask = 0;
            dependency.DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit;
            dependency.DstAccessMask = AccessFlags.ColorAttachmentWriteBit;
            RenderPassCreateInfo renderPassInfo = new RenderPassCreateInfo();
            renderPassInfo.SType = StructureType.RenderPassCreateInfo;
            renderPassInfo.AttachmentCount = 1;
            renderPassInfo.PAttachments = &colorAttachment;
            renderPassInfo.SubpassCount = 1;
            renderPassInfo.PSubpasses = &subpass;
            renderPassInfo.DependencyCount = 1;
            renderPassInfo.PDependencies = &dependency;
            if (vk.CreateRenderPass(device, &renderPassInfo, null, out renderPass) != Result.Success)
            {
                throw new Exception("Failed to create render pass");
            }
        }

        private void CreateGraphicsPipeline()
        {
            string vertShaderCode = @"
#version 450
layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inColor;
layout(location = 0) out vec3 fragColor;
layout(push_constant) uniform PushConstants {
    mat4 model;
    mat4 view;
    mat4 proj;
} push;
void main() {
    gl_Position = push.proj * push.view * push.model * vec4(inPosition, 1.0);
    fragColor = inColor;
}
";
            string fragShaderCode = @"
#version 450
layout(location = 0) in vec3 fragColor;
layout(location = 0) out vec4 outColor;
void main() {
    outColor = vec4(fragColor, 1.0);
}
";
            CompilationResult vertResult = shaderc.CompileIntoSpv(vertShaderCode, ShaderKind.VertexShader, "vertex.glsl");
            if (vertResult.Result != CompilationStatus.Success)
            {
                throw new Exception("Vertex shader compilation failed: " + vertResult.Errors);
            }
            byte[] vertSpv = vertResult.Code;
            CompilationResult fragResult = shaderc.CompileIntoSpv(fragShaderCode, ShaderKind.FragmentShader, "fragment.glsl");
            if (fragResult.Result != CompilationStatus.Success)
            {
                throw new Exception("Fragment shader compilation failed: " + fragResult.Errors);
            }
            byte[] fragSpv = fragResult.Code;
            ShaderModuleCreateInfo vertShaderModuleCreateInfo = new ShaderModuleCreateInfo();
            vertShaderModuleCreateInfo.SType = StructureType.ShaderModuleCreateInfo;
            vertShaderModuleCreateInfo.CodeSize = (nuint)vertSpv.Length;
            fixed (byte* p = vertSpv)
            {
                vertShaderModuleCreateInfo.PCode = (uint*)p;
            }
            ShaderModule vertShaderModule;
            vk.CreateShaderModule(device, &vertShaderModuleCreateInfo, null, out vertShaderModule);
            ShaderModuleCreateInfo fragShaderModuleCreateInfo = new ShaderModuleCreateInfo();
            fragShaderModuleCreateInfo.SType = StructureType.ShaderModuleCreateInfo;
            fragShaderModuleCreateInfo.CodeSize = (nuint)fragSpv.Length;
            fixed (byte* p = fragSpv)
            {
                fragShaderModuleCreateInfo.PCode = (uint*)p;
            }
            ShaderModule fragShaderModule;
            vk.CreateShaderModule(device, &fragShaderModuleCreateInfo, null, out fragShaderModule);
            PipelineShaderStageCreateInfo vertShaderStageInfo = new PipelineShaderStageCreateInfo();
            vertShaderStageInfo.SType = StructureType.PipelineShaderStageCreateInfo;
            vertShaderStageInfo.Stage = ShaderStageFlags.VertexBit;
            vertShaderStageInfo.Module = vertShaderModule;
            vertShaderStageInfo.PName = (byte*)&"main";
            PipelineShaderStageCreateInfo fragShaderStageInfo = new PipelineShaderStageCreateInfo();
            fragShaderStageInfo.SType = StructureType.PipelineShaderStageCreateInfo;
            fragShaderStageInfo.Stage = ShaderStageFlags.FragmentBit;
            fragShaderStageInfo.Module = fragShaderModule;
            fragShaderStageInfo.PName = (byte*)&"main";
            PipelineShaderStageCreateInfo[] shaderStages = { vertShaderStageInfo, fragShaderStageInfo };
            VertexInputBindingDescription bindingDescription = new VertexInputBindingDescription();
            bindingDescription.Binding = 0;
            bindingDescription.Stride = (uint)Marshal.SizeOf<Vertex>();
            bindingDescription.InputRate = VertexInputRate.Vertex;
            VertexInputAttributeDescription[] attributeDescriptions = new VertexInputAttributeDescription[2];
            attributeDescriptions[0].Binding = 0;
            attributeDescriptions[0].Location = 0;
            attributeDescriptions[0].Format = Format.R32G32B32Sfloat;
            attributeDescriptions[0].Offset = 0;
            attributeDescriptions[1].Binding = 0;
            attributeDescriptions[1].Location = 1;
            attributeDescriptions[1].Format = Format.R32G32B32Sfloat;
            attributeDescriptions[1].Offset = 12;
            PipelineVertexInputStateCreateInfo vertexInputInfo = new PipelineVertexInputStateCreateInfo();
            vertexInputInfo.SType = StructureType.PipelineVertexInputStateCreateInfo;
            vertexInputInfo.VertexBindingDescriptionCount = 1;
            vertexInputInfo.PVertexBindingDescriptions = &bindingDescription;
            vertexInputInfo.VertexAttributeDescriptionCount = 2;
            vertexInputInfo.PVertexAttributeDescriptions = attributeDescriptions;
            PipelineInputAssemblyStateCreateInfo inputAssembly = new PipelineInputAssemblyStateCreateInfo();
            inputAssembly.SType = StructureType.PipelineInputAssemblyStateCreateInfo;
            inputAssembly.Topology = PrimitiveTopology.TriangleList;
            inputAssembly.PrimitiveRestartEnable = false;
            Viewport viewport = new Viewport();
            viewport.X = 0.0f;
            viewport.Y = 0.0f;
            viewport.Width = swapChainExtent.Width;
            viewport.Height = swapChainExtent.Height;
            viewport.MinDepth = 0.0f;
            viewport.MaxDepth = 1.0f;
            Rect2D scissor = new Rect2D();
            scissor.Offset = new Offset2D(0, 0);
            scissor.Extent = swapChainExtent;
            PipelineViewportStateCreateInfo viewportState = new PipelineViewportStateCreateInfo();
            viewportState.SType = StructureType.PipelineViewportStateCreateInfo;
            viewportState.ViewportCount = 1;
            viewportState.PViewports = &viewport;
            viewportState.ScissorCount = 1;
            viewportState.PScissors = &scissor;
            PipelineRasterizationStateCreateInfo rasterizer = new PipelineRasterizationStateCreateInfo();
            rasterizer.SType = StructureType.PipelineRasterizationStateCreateInfo;
            rasterizer.DepthClampEnable = false;
            rasterizer.RasterizerDiscardEnable = false;
            rasterizer.PolygonMode = PolygonMode.Fill;
            rasterizer.LineWidth = 1.0f;
            rasterizer.CullMode = CullModeFlags.BackBit;
            rasterizer.FrontFace = FrontFace.CounterClockwise;
            rasterizer.DepthBiasEnable = false;
            PipelineMultisampleStateCreateInfo multisampling = new PipelineMultisampleStateCreateInfo();
            multisampling.SType = StructureType.PipelineMultisampleStateCreateInfo;
            multisampling.SampleShadingEnable = false;
            multisampling.RasterizationSamples = SampleCountFlags.Count1Bit;
            PipelineColorBlendAttachmentState colorBlendAttachment = new PipelineColorBlendAttachmentState();
            colorBlendAttachment.ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit;
            colorBlendAttachment.BlendEnable = false;
            PipelineColorBlendStateCreateInfo colorBlending = new PipelineColorBlendStateCreateInfo();
            colorBlending.SType = StructureType.PipelineColorBlendStateCreateInfo;
            colorBlending.LogicOpEnable = false;
            colorBlending.LogicOp = LogicOp.Copy;
            colorBlending.AttachmentCount = 1;
            colorBlending.PAttachments = &colorBlendAttachment;
            colorBlending.BlendConstants[0] = 0.0f;
            colorBlending.BlendConstants[1] = 0.0f;
            colorBlending.BlendConstants[2] = 0.0f;
            colorBlending.BlendConstants[3] = 0.0f;
            PushConstantRange pushConstantRange = new PushConstantRange();
            pushConstantRange.StageFlags = ShaderStageFlags.VertexBit;
            pushConstantRange.Offset = 0;
            pushConstantRange.Size = (uint)(Marshal.SizeOf<Matrix4x4>() * 3);
            PipelineLayoutCreateInfo pipelineLayoutInfo = new PipelineLayoutCreateInfo();
            pipelineLayoutInfo.SType = StructureType.PipelineLayoutCreateInfo;
            pipelineLayoutInfo.SetLayoutCount = 0;
            pipelineLayoutInfo.PushConstantRangeCount = 1;
            pipelineLayoutInfo.PPushConstantRanges = &pushConstantRange;
            vk.CreatePipelineLayout(device, &pipelineLayoutInfo, null, out pipelineLayout);
            GraphicsPipelineCreateInfo pipelineInfo = new GraphicsPipelineCreateInfo();
            pipelineInfo.SType = StructureType.GraphicsPipelineCreateInfo;
            pipelineInfo.StageCount = 2;
            pipelineInfo.PStages = shaderStages;
            pipelineInfo.PVertexInputState = &vertexInputInfo;
            pipelineInfo.PInputAssemblyState = &inputAssembly;
            pipelineInfo.PViewportState = &viewportState;
            pipelineInfo.PRasterizationState = &rasterizer;
            pipelineInfo.PMultisampleState = &multisampling;
            pipelineInfo.PColorBlendState = &colorBlending;
            pipelineInfo.Layout = pipelineLayout;
            pipelineInfo.RenderPass = renderPass;
            pipelineInfo.Subpass = 0;
            pipelineInfo.BasePipelineHandle = default;
            pipelineInfo.BasePipelineIndex = -1;
            vk.CreateGraphicsPipelines(device, default, 1, &pipelineInfo, null, out graphicsPipeline);
            vk.DestroyShaderModule(device, vertShaderModule, null);
            vk.DestroyShaderModule(device, fragShaderModule, null);
        }

        private void CreateFramebuffers()
        {
            swapChainFramebuffers = new Framebuffer[swapChainImageViews.Length];
            for (int i = 0; i < swapChainImageViews.Length; i++)
            {
                ImageView[] attachments = { swapChainImageViews[i] };
                FramebufferCreateInfo framebufferInfo = new FramebufferCreateInfo();
                framebufferInfo.SType = StructureType.FramebufferCreateInfo;
                framebufferInfo.RenderPass = renderPass;
                framebufferInfo.AttachmentCount = 1;
                framebufferInfo.PAttachments = attachments;
                framebufferInfo.Width = swapChainExtent.Width;
                framebufferInfo.Height = swapChainExtent.Height;
                framebufferInfo.Layers = 1;
                vk.CreateFramebuffer(device, &framebufferInfo, null, out swapChainFramebuffers[i]);
            }
        }

        private void CreateCommandPool()
        {
            QueueFamilyIndices queueFamilyIndices = FindQueueFamilies(physicalDevice);
            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo();
            poolInfo.SType = StructureType.CommandPoolCreateInfo;
            poolInfo.QueueFamilyIndex = queueFamilyIndices.graphicsFamily.Value;
            vk.CreateCommandPool(device, &poolInfo, null, out commandPool);
        }

        private void CreateVertexBuffer()
        {
            Vertex[] vertices = {
                new Vertex { Position = new Vector3(-0.5f, -0.5f, 0.5f), Color = new Vector3(1, 0, 0) },
                new Vertex { Position = new Vector3(0.5f, -0.5f, 0.5f), Color = new Vector3(0, 1, 0) },
                new Vertex { Position = new Vector3(0.5f, 0.5f, 0.5f), Color = new Vector3(0, 0, 1) },
                new Vertex { Position = new Vector3(-0.5f, 0.5f, 0.5f), Color = new Vector3(1, 1, 0) },
                new Vertex { Position = new Vector3(-0.5f, -0.5f, -0.5f), Color = new Vector3(1, 0, 1) },
                new Vertex { Position = new Vector3(0.5f, -0.5f, -0.5f), Color = new Vector3(0, 1, 1) },
                new Vertex { Position = new Vector3(0.5f, 0.5f, -0.5f), Color = new Vector3(1, 1, 1) },
                new Vertex { Position = new Vector3(-0.5f, 0.5f, -0.5f), Color = new Vector3(0.5f, 0.5f, 0.5f) }
            };
            ulong bufferSize = (ulong)(vertices.Length * Marshal.SizeOf<Vertex>());
            CreateBuffer(bufferSize, BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out vertexBuffer, out vertexBufferMemory);
            void* data;
            vk.MapMemory(device, vertexBufferMemory, 0, bufferSize, 0, &data);
            fixed (Vertex* p = vertices)
            {
                Buffer.MemoryCopy(p, data, bufferSize);
            }
            vk.UnmapMemory(device, vertexBufferMemory);
        }

        private void CreateIndexBuffer()
        {
            indices = new uint[] {
                0, 1, 2, 2, 3, 0,
                4, 5, 6, 6, 7, 4,
                0, 1, 5, 5, 4, 0,
                1, 2, 6, 6, 5, 1,
                2, 3, 7, 7, 6, 2,
                3, 0, 4, 4, 7, 3
            };
            ulong bufferSize = (ulong)(indices.Length * sizeof(uint));
            CreateBuffer(bufferSize, BufferUsageFlags.IndexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out indexBuffer, out indexBufferMemory);
            void* data;
            vk.MapMemory(device, indexBufferMemory, 0, bufferSize, 0, &data);
            fixed (uint* p = indices)
            {
                Buffer.MemoryCopy(p, data, bufferSize);
            }
            vk.UnmapMemory(device, indexBufferMemory);
        }

        private void CreateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, out Buffer buffer, out DeviceMemory bufferMemory)
        {
            BufferCreateInfo bufferInfo = new BufferCreateInfo();
            bufferInfo.SType = StructureType.BufferCreateInfo;
            bufferInfo.Size = size;
            bufferInfo.Usage = usage;
            bufferInfo.SharingMode = SharingMode.Exclusive;
            vk.CreateBuffer(device, &bufferInfo, null, out buffer);
            MemoryRequirements memRequirements;
            vk.GetBufferMemoryRequirements(device, buffer, &memRequirements);
            MemoryAllocateInfo allocInfo = new MemoryAllocateInfo();
            allocInfo.SType = StructureType.MemoryAllocateInfo;
            allocInfo.AllocationSize = memRequirements.Size;
            allocInfo.MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties);
            vk.AllocateMemory(device, &allocInfo, null, out bufferMemory);
            vk.BindBufferMemory(device, buffer, bufferMemory, 0);
        }

        private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
        {
            PhysicalDeviceMemoryProperties memProperties;
            vk.GetPhysicalDeviceMemoryProperties(physicalDevice, &memProperties);
            for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                if ((typeFilter & (1 << (int)i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
                {
                    return i;
                }
            }
            throw new Exception("Failed to find suitable memory type");
        }

        private void CreateCommandBuffers()
        {
            CommandBufferAllocateInfo allocInfo = new CommandBufferAllocateInfo();
            allocInfo.SType = StructureType.CommandBufferAllocateInfo;
            allocInfo.CommandPool = commandPool;
            allocInfo.Level = CommandBufferLevel.Primary;
            allocInfo.CommandBufferCount = (uint)swapChainFramebuffers.Length;
            commandBuffers = new CommandBuffer[swapChainFramebuffers.Length];
            vk.AllocateCommandBuffers(device, &allocInfo, commandBuffers);
        }

        private void CreateSyncObjects()
        {
            imageAvailableSemaphores = new Semaphore[maxFramesInFlight];
            renderFinishedSemaphores = new Semaphore[maxFramesInFlight];
            inFlightFences = new Fence[maxFramesInFlight];
            SemaphoreCreateInfo semaphoreInfo = new SemaphoreCreateInfo();
            semaphoreInfo.SType = StructureType.SemaphoreCreateInfo;
            FenceCreateInfo fenceInfo = new FenceCreateInfo();
            fenceInfo.SType = StructureType.FenceCreateInfo;
            fenceInfo.Flags = FenceCreateFlags.SignaledBit;
            for (int i = 0; i < maxFramesInFlight; i++)
            {
                vk.CreateSemaphore(device, &semaphoreInfo, null, out imageAvailableSemaphores[i]);
                vk.CreateSemaphore(device, &semaphoreInfo, null, out renderFinishedSemaphores[i]);
                vk.CreateFence(device, &fenceInfo, null, out inFlightFences[i]);
            }
        }

        private void RecordCommandBuffer(CommandBuffer commandBuffer, uint imageIndex)
        {
            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            beginInfo.SType = StructureType.CommandBufferBeginInfo;
            vk.BeginCommandBuffer(commandBuffer, &beginInfo);
            RenderPassBeginInfo renderPassInfo = new RenderPassBeginInfo();
            renderPassInfo.SType = StructureType.RenderPassBeginInfo;
            renderPassInfo.RenderPass = renderPass;
            renderPassInfo.Framebuffer = swapChainFramebuffers[imageIndex];
            renderPassInfo.RenderArea.Offset = new Offset2D(0, 0);
            renderPassInfo.RenderArea.Extent = swapChainExtent;
            ClearValue clearColor = new ClearValue();
            clearColor.Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 1.0f);
            renderPassInfo.ClearValueCount = 1;
            renderPassInfo.PClearValues = &clearColor;
            vk.CmdBeginRenderPass(commandBuffer, &renderPassInfo, SubpassContents.Inline);
            vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, graphicsPipeline);
            ulong[] offsets = { 0 };
            vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &vertexBuffer, offsets);
            vk.CmdBindIndexBuffer(commandBuffer, indexBuffer, 0, IndexType.Uint32);
            Matrix4x4 model = Matrix4x4.CreateRotationY((float)time);
            Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)swapChainExtent.Width / swapChainExtent.Height, 0.1f, 10.0f);
            proj.M22 *= -1;
            Matrix4x4[] matrices = { model, view, proj };
            fixed (Matrix4x4* p = matrices)
            {
                vk.CmdPushConstants(commandBuffer, pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)(Marshal.SizeOf<Matrix4x4>() * 3), p);
            }
            vk.CmdDrawIndexed(commandBuffer, (uint)indices.Length, 1, 0, 0, 0);
            vk.CmdEndRenderPass(commandBuffer);
            vk.EndCommandBuffer(commandBuffer);
        }

        public override void RenderFrame(double deltaTime)
        {
            time += deltaTime;
            vk.WaitForFences(device, 1, inFlightFences[currentFrame], true, ulong.MaxValue);
            uint imageIndex;
            Result result = vk.AcquireNextImageKHR(device, swapChain, ulong.MaxValue, imageAvailableSemaphores[currentFrame], default, out imageIndex);
            if (result == Result.ErrorOutOfDateKhr)
            {
                RecreateSwapChain();
                return;
            }
            else if (result != Result.Success && result != Result.SuboptimalKhr)
            {
                throw new Exception("Failed to acquire swap chain image");
            }
            vk.ResetFences(device, 1, inFlightFences[currentFrame]);
            vk.ResetCommandBuffer(commandBuffers[imageIndex], 0);
            RecordCommandBuffer(commandBuffers[imageIndex], imageIndex);
            SubmitInfo submitInfo = new SubmitInfo();
            submitInfo.SType = StructureType.SubmitInfo;
            submitInfo.WaitSemaphoreCount = 1;
            submitInfo.PWaitSemaphores = &imageAvailableSemaphores[currentFrame];
            PipelineStageFlags[] waitStages = { PipelineStageFlags.ColorAttachmentOutputBit };
            submitInfo.PWaitDstStageMask = waitStages;
            submitInfo.CommandBufferCount = 1;
            submitInfo.PCommandBuffers = &commandBuffers[imageIndex];
            submitInfo.SignalSemaphoreCount = 1;
            submitInfo.PSignalSemaphores = &renderFinishedSemaphores[currentFrame];
            vk.QueueSubmit(graphicsQueue, 1, &submitInfo, inFlightFences[currentFrame]);
            PresentInfoKHR presentInfo = new PresentInfoKHR();
            presentInfo.SType = StructureType.PresentInfoKhr;
            presentInfo.WaitSemaphoreCount = 1;
            presentInfo.PWaitSemaphores = &renderFinishedSemaphores[currentFrame];
            presentInfo.SwapchainCount = 1;
            presentInfo.PSwapchains = &swapChain;
            presentInfo.PImageIndices = &imageIndex;
            result = vk.QueuePresentKHR(presentQueue, &presentInfo);
            if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
            {
                RecreateSwapChain();
            }
            else if (result != Result.Success)
            {
                throw new Exception("Failed to present swap chain image");
            }
            currentFrame = (currentFrame + 1) % maxFramesInFlight;
        }

        private void RecreateSwapChain()
        {
            vk.DeviceWaitIdle(device);
            CleanupSwapChain();
            CreateSwapChain();
            CreateImageViews();
            CreateFramebuffers();
        }

        private void CleanupSwapChain()
        {
            foreach (var framebuffer in swapChainFramebuffers)
            {
                vk.DestroyFramebuffer(device, framebuffer, null);
            }
            foreach (var imageView in swapChainImageViews)
            {
                vk.DestroyImageView(device, imageView, null);
            }
            vk.DestroySwapchainKHR(device, swapChain, null);
        }

        public override void Close()
        {
            vk.DeviceWaitIdle(device);
            CleanupSwapChain();
            vk.DestroyBuffer(device, indexBuffer, null);
            vk.FreeMemory(device, indexBufferMemory, null);
            vk.DestroyBuffer(device, vertexBuffer, null);
            vk.FreeMemory(device, vertexBufferMemory, null);
            for (int i = 0; i < maxFramesInFlight; i++)
            {
                vk.DestroySemaphore(device, renderFinishedSemaphores[i], null);
                vk.DestroySemaphore(device, imageAvailableSemaphores[i], null);
                vk.DestroyFence(device, inFlightFences[i], null);
            }
            vk.DestroyCommandPool(device, commandPool, null);
            vk.DestroyPipeline(device, graphicsPipeline, null);
            vk.DestroyPipelineLayout(device, pipelineLayout, null);
            vk.DestroyRenderPass(device, renderPass, null);
            vk.DestroyDevice(device, null);
            vk.DestroySurfaceKHR(instance, surface, null);
            vk.DestroyInstance(instance, null);
        }

        private struct QueueFamilyIndices
        {
            public uint? graphicsFamily = null;
            public uint? presentFamily = null;
            public bool IsComplete()
            {
                return graphicsFamily.HasValue && presentFamily.HasValue;
            }
        }

        private struct SwapChainSupportDetails
        {
            public SurfaceCapabilitiesKHR capabilities;
            public SurfaceFormatKHR[] formats;
            public PresentModeKHR[] presentModes;
        }

        private struct Vertex
        {
            public Vector3 Position;
            public Vector3 Color;
        }
    }
}
