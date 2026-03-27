using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.SEComponents;
using SaturnEngine.SEMath;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using Silk.NET.WGL;
using System.Runtime.InteropServices;

namespace SaturnEngine.SEGraphics
{
    public unsafe class SE2DOpenGLRender : Render
    {
        delegate bool APIEntryPoint(int n);
        event APIEntryPoint wglSwapIntervalEXT;
        GL gl;
        Glfw g;
        WGL wgl;
        bool renderUI = false;

        // 渲染相关字段
        private Dictionary<uint, uint> textureCache; // 纹理缓存 [SEImageFile hash -> OpenGL texture ID]
        private List<RenderableSprite> sceneSprites; // 场景精灵列表
        private List<RenderableUIControl> uiControls; // UI控件列表
        private uint shaderProgram;
        private uint vao, vbo, ebo;

        // 场景和UI引用
        private Scene currentScene;
        private UIScene currentUIScene;

        public override bool CheckSupport(Feature f)
        {
            switch (f)
            {
                case Feature.Sync:
                    return true;
                case Feature.HDR:
                    return false;
                case Feature.DolbyVision:
                    return false;
                default:
                    return false;
            }
        }

        public override void Close()
        {
            // 清理OpenGL资源
            if (gl != null)
            {
                if (shaderProgram != 0)
                    gl.DeleteProgram(shaderProgram);
                if (vao != 0)
                    gl.DeleteVertexArray(vao);
                if (vbo != 0)
                    gl.DeleteBuffer(vbo);
                if (ebo != 0)
                    gl.DeleteBuffer(ebo);

                // 清理纹理
                foreach (var texture in textureCache.Values)
                {
                    gl.DeleteTexture(texture);
                }
                textureCache.Clear();
            }
        }

        public override bool CreateDevice(int index = 0)
        {
            //SELogger.Log("CD OpenGL 2D");
            InitializeOpenGLResources();
            return true;
        }

        public override void DestroyDevice()
        {
            Close();
        }

        public override string[] GetDeviceNames()
        {
            return new string[] { "Default Device" };
        }

        public override void Initialize()
        {
            g = Glfw.GetApi();
            //g.MakeContextCurrent((WindowHandle*)(GVariables.MainWindow.GetWindowHandle().ToPointer()));
            gl = GL.GetApi(g.GetProcAddress);
            

            if (GVariables.OS == OS.Windows)
            {
                wgl = WGL.GetApi();
                wglSwapIntervalEXT = Marshal.GetDelegateForFunctionPointer<APIEntryPoint>(wgl.GetProcAddress("wglSwapIntervalEXT"));
            }
            else
            {
                wglSwapIntervalEXT = (n) => { return false; };
            }
            SetFeature(Feature.Sync, false); // Disable VSync
            string vendor = Helper.PTRGetString(gl.GetString(StringName.Vendor));
            string renderer = Helper.PTRGetString(gl.GetString(StringName.Renderer));

            //Console.WriteLine($"Graphics Card Vendor: {vendor}");
            //Console.WriteLine($"Graphics Card Model: {renderer}");
            //SaturnEngine.Platform.Global.SetGPU(vendor);

            // 初始化渲染资源
            textureCache = new Dictionary<uint, uint>();
            sceneSprites = new List<RenderableSprite>();
            uiControls = new List<RenderableUIControl>();
        }

        public override void PrepareFrame(double deltaTime)
        {
            // 在非主线程准备渲染数据
            // 这里可以处理一些不涉及OpenGL状态的操作，比如更新位置计算等
            UpdateSpritePositions(deltaTime);
            UpdateUIControlPositions(deltaTime);
        }

        public override void RenderFrame(double deltaTime)
        {
            //SELogger.Log("In render");
            // 在主线程执行OpenGL渲染
            //SELogger.Log("渲染帧");
            gl.ClearColor(0.3f, 0.3f, 0.3f, 1);
            gl.Clear(ClearBufferMask.ColorBufferBit);

            // 设置OpenGL状态
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            gl.Disable(EnableCap.DepthTest);

            // 使用着色器程序
            gl.UseProgram(shaderProgram);

            // 渲染场景精灵
            RenderSceneSprites();

            // 如果有UI场景，渲染UI控件
            if (renderUI && currentUIScene != null)
            {
                RenderUIControls();
            }

            // 重置OpenGL状态
            gl.Disable(EnableCap.Blend);
            gl.Enable(EnableCap.DepthTest);
        }

        public override void SetFeature(Feature f, bool enable)
        {
            switch (f)
            {
                case Feature.Sync:
                    if (enable)
                        wglSwapIntervalEXT.Invoke(1);
                    else
                        wglSwapIntervalEXT.Invoke(0);
                    break;
                case Feature.HDR:
                    break;
                case Feature.DolbyVision:
                    break;
                default:
                    break;
            }
        }

        public override void SetPosition(int x, int y)
        {
            // 可以更新视口位置
        }

        public override void SetScene(int index)
        {
            //var s = GVariables.ThisGame.ThisScenes[index];
            //currentScene = s;
            //LoadSceneSprites(s);
        }

        public override void SetSize(int width, int height)
        {
            // 更新视口大小
            gl.Viewport(0, 0, (uint)width, (uint)height);

            // 更新正交投影矩阵
            UpdateOrthoProjection(width, height);
        }

        public override void SetUIScene(bool enable)
        {
            /*
            renderUI = enable;
            if (enable && GVariables.ThisGame.UIScene != null)
            {
                currentUIScene = GVariables.ThisGame.UIScene;
                LoadUIControls(currentUIScene);
            }
            */
        }

        #region 私有方法

        private void InitializeOpenGLResources()
        {
            // 编译着色器
            string vertexShaderSource = @"
                #version 330 core
                layout (location = 0) in vec2 aPosition;
                layout (location = 1) in vec2 aTexCoord;
                
                out vec2 TexCoord;
                
                uniform mat4 projection;
                uniform mat4 model;
                
                void main()
                {
                    gl_Position = projection * model * vec4(aPosition, 0.0, 1.0);
                    TexCoord = aTexCoord;
                }";

            string fragmentShaderSource = @"
                #version 330 core
                out vec4 FragColor;
                
                in vec2 TexCoord;
                
                uniform sampler2D texture1;
                uniform vec4 color;
                
                void main()
                {
                    vec4 texColor = texture(texture1, TexCoord);
                    FragColor = texColor * color;
                }";

            uint vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
            uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

            shaderProgram = gl.CreateProgram();
            gl.AttachShader(shaderProgram, vertexShader);
            gl.AttachShader(shaderProgram, fragmentShader);
            gl.LinkProgram(shaderProgram);

            gl.DeleteShader(vertexShader);
            gl.DeleteShader(fragmentShader);

            // 设置顶点数据
            float[] vertices = {
                // 位置          // 纹理坐标
                0.5f,  0.5f,     1.0f, 1.0f,   // 右上
                0.5f, -0.5f,     1.0f, 0.0f,   // 右下
                -0.5f, -0.5f,    0.0f, 0.0f,   // 左下
                -0.5f,  0.5f,    0.0f, 1.0f    // 左上
            };

            uint[] indices = {
                0, 1, 3,   // 第一个三角形
                1, 2, 3    // 第二个三角形
            };

            // 生成VAO、VBO、EBO
            vao = gl.GenVertexArray();
            vbo = gl.GenBuffer();
            ebo = gl.GenBuffer();

            gl.BindVertexArray(vao);

            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            gl.BufferData<float>(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.StaticDraw);

            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
            gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);

            // 位置属性
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            gl.EnableVertexAttribArray(0);

            // 纹理坐标属性
            gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            gl.EnableVertexAttribArray(1);

            gl.BindVertexArray(0);
        }

        private uint CompileShader(ShaderType type, string source)
        {
            uint shader = gl.CreateShader(type);
            gl.ShaderSource(shader, source);
            gl.CompileShader(shader);

            gl.GetShader(shader, ShaderParameterName.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = gl.GetShaderInfoLog(shader);
                Console.WriteLine($"ERROR::SHADER::COMPILATION_FAILED::{type}\n{infoLog}");
            }

            return shader;
        }

        private void UpdateOrthoProjection(int width, int height)
        {
            gl.UseProgram(shaderProgram);

            // 创建正交投影矩阵
            float[] projection = new float[16];
            projection[0] = 2.0f / width;  // X缩放
            projection[5] = 2.0f / height; // Y缩放  
            projection[10] = 1.0f;         // Z缩放
            projection[12] = -1.0f;        // X平移
            projection[13] = -1.0f;        // Y平移
            projection[15] = 1.0f;         // 齐次坐标

            // 设置投影矩阵uniform
            int projectionLoc = gl.GetUniformLocation(shaderProgram, "projection");
            gl.UniformMatrix4(projectionLoc, 1, false, projection);
        }

        private void LoadSceneSprites(Scene scene)
        {
            sceneSprites.Clear();

            foreach (var gameObject in scene.ThisGameObjects)
            {
                var components = ((IComponent)gameObject).Components;
                var spirit2DComponent = components.Search(SEComponentType.Spirit2D) as Spirit2D;

                if (spirit2DComponent != null && spirit2DComponent.BaseSpirit != null && spirit2DComponent.BaseSpirit.IsLoaded)
                {
                    var transform = ((ITransform)gameObject).Transform;
                    var sprite = new RenderableSprite
                    {
                        GameObject = gameObject,
                        Spirit = spirit2DComponent.BaseSpirit,
                        Transform = transform,
                        TextureID = LoadTexture(spirit2DComponent.BaseSpirit.BaseImage)
                    };

                    sceneSprites.Add(sprite);
                }
            }
        }

        private void LoadUIControls(UIScene uiScene)
        {
            uiControls.Clear();

            // 这里需要根据实际的UI控件结构来遍历
            // 假设uiScene.Controls是SEControls类型
            if (uiScene.Controls != null)
            {
                foreach (var control in uiScene.Controls.Controls)
                {
                    if (control.Spirit != null && control.Spirit.IsLoaded)
                    {
                        var uiControl = new RenderableUIControl
                        {
                            Control = control,
                            Spirit = control.Spirit,
                            TextureID = LoadTexture(control.Spirit.BaseImage)
                        };

                        uiControls.Add(uiControl);
                    }
                }
            }
        }

        private uint LoadTexture(SEImageFile imageFile)
        {
            if (imageFile == null || !imageFile.IsLoaded)
                return 0;

            uint hash = (uint)imageFile.GetHashCode();

            if (textureCache.ContainsKey(hash))
                return textureCache[hash];

            var image = imageFile.GetImage();
            if (image == null)
                return 0;

            // 转换为RGBA格式
            var rgbaImage = image.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
            byte[] pixelData = new byte[rgbaImage.Width * rgbaImage.Height * 4];
            rgbaImage.CopyPixelDataTo(pixelData);

            // 生成OpenGL纹理
            uint textureID = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, textureID);

            // 设置纹理参数
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

            // 上传纹理数据
            fixed (byte* ptr = pixelData)
            {
                gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, (uint)rgbaImage.Width,
                             (uint)rgbaImage.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }

            gl.GenerateMipmap(TextureTarget.Texture2D);
            textureCache[hash] = textureID;

            return textureID;
        }

        private void UpdateSpritePositions(double deltaTime)
        {
            foreach (var sprite in sceneSprites)
            {
                // 根据Transform更新精灵的位置、旋转、缩放
                // 这里需要根据实际的Transform结构来计算最终的渲染矩阵
                var transform = sprite.Transform;
                var baseVector = transform.BaseVector;

                // 简单的2D变换计算
                sprite.RenderPosition = new Vector2D(baseVector.X, baseVector.Y);
                sprite.RenderSize = new Vector2D(100, 100); // 根据精灵实际大小设置
            }
        }

        private void UpdateUIControlPositions(double deltaTime)
        {
            //GVariables.ThisGame.UIScene.Controls.Flush(GVariables.MainWindow.Size);
            //SELogger.Log("PROC UI");
            foreach (var uiControl in uiControls)
            {
                var v = uiControl.Control.Position;
                if (v.HasValue)
                {
                    var rect = v.Value;
                    uiControl.RenderPosition = new Vector2D(rect[0][0], rect[0][1]);
                    uiControl.RenderSize = new Vector2D(rect[1][0] - rect[0][0], rect[1][1] - rect[0][1]);
                    //SELogger.Log($"UI Control {uiControl.Control.Name} Position: {uiControl.RenderPosition}, Size: {uiControl.RenderSize}");
                }
            }
        }

        private void RenderSceneSprites()
        {
            gl.BindVertexArray(vao);

            foreach (var sprite in sceneSprites)
            {
                if (sprite.TextureID == 0) continue;

                // 绑定纹理
                gl.ActiveTexture(TextureUnit.Texture0);
                gl.BindTexture(TextureTarget.Texture2D, sprite.TextureID);

                // 计算模型矩阵
                float[] modelMatrix = CalculateModelMatrix(sprite.RenderPosition, sprite.RenderSize, 0f);

                // 设置uniform
                int modelLoc = gl.GetUniformLocation(shaderProgram, "model");
                gl.UniformMatrix4(modelLoc, 1, false, modelMatrix);

                int colorLoc = gl.GetUniformLocation(shaderProgram, "color");
                gl.Uniform4(colorLoc, 1.0f, 1.0f, 1.0f, 1.0f); // 白色

                // 绘制
                gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
            }

            gl.BindVertexArray(0);
        }

        private void RenderUIControls()
        {
            gl.BindVertexArray(vao);

            foreach (var uiControl in uiControls)
            {
                if (uiControl.TextureID == 0) continue;

                // 绑定纹理
                gl.ActiveTexture(TextureUnit.Texture0);
                gl.BindTexture(TextureTarget.Texture2D, uiControl.TextureID);

                // 计算模型矩阵
                float[] modelMatrix = CalculateModelMatrix(uiControl.RenderPosition, uiControl.RenderSize, 0f);

                // 设置uniform
                int modelLoc = gl.GetUniformLocation(shaderProgram, "model");
                gl.UniformMatrix4(modelLoc, 1, false, modelMatrix);

                int colorLoc = gl.GetUniformLocation(shaderProgram, "color");
                gl.Uniform4(colorLoc, 1.0f, 1.0f, 1.0f, 1.0f); // 白色

                // 绘制
                gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
            }

            gl.BindVertexArray(0);
        }

        private float[] CalculateModelMatrix(Vector2D position, Vector2D size, float rotation)
        {
            float[] model = new float[16];

            // 平移
            model[0] = (float)size.X;           // X缩放
            model[5] = (float)size.Y;           // Y缩放
            model[10] = 1.0f;            // Z缩放
            model[12] = (float)position.X; // X平移
            model[13] = (float)position.Y; // Y平移
            model[15] = 1.0f;            // 齐次坐标

            // 注意：这是一个简化的2D变换矩阵，如果需要旋转需要更复杂的计算

            return model;
        }

        #endregion

        #region 辅助类

        private class RenderableSprite
        {
            public GameObject GameObject { get; set; }
            public SESpirit Spirit { get; set; }
            public Transform Transform { get; set; }
            public uint TextureID { get; set; }
            public Vector2D RenderPosition { get; set; }
            public Vector2D RenderSize { get; set; }
        }

        private class RenderableUIControl
        {
            public SEControl Control { get; set; }
            public SESpirit Spirit { get; set; }
            public uint TextureID { get; set; }
            public Vector2D RenderPosition { get; set; }
            public Vector2D RenderSize { get; set; }
        }

        #endregion
    }
}
