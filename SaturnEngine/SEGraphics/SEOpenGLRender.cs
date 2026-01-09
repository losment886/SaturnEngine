using SaturnEngine.Asset;
using SaturnEngine.Global;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using Silk.NET.WGL;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;
using static SaturnEngine.SEMath.Helper;

namespace SaturnEngine.SEGraphics
{
    public unsafe class SEOpenGLRender : Render
    {
        GL gl;
        Glfw g;
        WGL wgl;
        WindowHandle* w;
        public override bool CreateDevice(int index = 0)
        {
            //OpenGL在这不支持自定义GPU


            ttu = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, ttu);
            gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            float[] borderColor = { 1.0f, 1.0f, 0.0f, 1.0f };
            //gl.TexParameterfv(GL_TEXTURE_2D, GL_TEXTURE_BORDER_COLOR, borderColor);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureBorderColor, borderColor);
            SEImageFile s = new SEImageFile();
            s.LoadImageFromFile(Console.ReadLine());
            SixLabors.ImageSharp.Image<Rgb24> i = s.GetImage().CloneAs<Rgb24>();
            byte[] bf = new byte[i.Width * i.Height * 3];//rgb24
            i.CopyPixelDataTo(bf);
            //igc.CopyPixelDataTo(bf);
            byte* ppc = (byte*)Marshal.AllocHGlobal(bf.Length).ToPointer();
            //i.Pixels = ppc;
            Parallel.For(0, bf.Length, (j) =>//copy to i.Pixels
            {
                ppc[j] = bf[j];
            });
            gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgb, (uint)i.Width, (uint)i.Height, 0, PixelFormat.Rgb, PixelType.UnsignedByte, ppc);
            gl.GenerateMipmap(TextureTarget.Texture2D);
            i.Dispose();
            Marshal.FreeHGlobal((nint)ppc);

            float[] vertices = {
//     ---- 位置 ----       ---- 颜色 ----     - 纹理坐标 -
     0.5f,  0.5f, 0.0f,   1.0f, 0.0f, 0.0f,   1.0f, 1.0f,   // 右上
     0.5f, -0.5f, 0.0f,   0.0f, 1.0f, 0.0f,   1.0f, 0.0f,   // 右下
    -0.5f, -0.5f, 0.0f,   0.0f, 0.0f, 1.0f,   0.0f, 0.0f,   // 左下
    -0.5f,  0.5f, 0.0f,   1.0f, 1.0f, 0.0f,   0.0f, 1.0f    // 左上
};
            vao = gl.GenVertexArray();
            vbo = gl.GenBuffer();
            gl.BufferData(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)vertices, BufferUsageARB.StaticDraw);
            string vs = @"
#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aColor;
layout (location = 2) in vec2 aTexCoord;

out vec3 ourColor;
out vec2 TexCoord;

void main()
{
    gl_Position = vec4(aPos, 1.0);
    ourColor = aColor;
    TexCoord = aTexCoord;
}";
            string fs = @"
#version 330 core
out vec4 FragColor;

in vec3 ourColor;
in vec2 TexCoord;

uniform sampler2D ourTexture;

void main()
{
    FragColor = texture(ourTexture, TexCoord);
}";
            sp = gl.CreateProgram();
            uint vsc = gl.CreateShader(ShaderType.VertexShader);
            uint fsc = gl.CreateShader(ShaderType.FragmentShader);
            gl.ShaderSource(vsc, vs);
            gl.CompileShader(vsc);
            gl.ShaderSource(fsc, fs);
            gl.CompileShader(fsc);
            gl.LinkProgram(sp);
            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, 8 * sizeof(float), (void*)(0 * sizeof(float)));
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(1, 3, GLEnum.Float, false, 8 * sizeof(float), (void*)(3 * sizeof(float)));
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(2, 2, GLEnum.Float, false, 8 * sizeof(float), (void*)(6 * sizeof(float)));
            gl.EnableVertexAttribArray(2);
            return true;
        }

        public override void PrepareFrame(double deltaTime)
        {

        }
        public override void DestroyDevice()
        {
            //throw new NotImplementedException();
        }

        public override string[] GetDeviceNames()
        {
            return new string[] { "Default Device" };
        }

        delegate bool APIEntryPoint(int n);
        event APIEntryPoint wglSwapIntervalEXT;
        private static uint vao, vbo, sp, ttu;

        public override void Initialize()
        {
            g = Glfw.GetApi();
            w = (WindowHandle*)(GVariables.MainWindow.GetWindowHandle().ToPointer());
            g.MakeContextCurrent(w);
            gl = GL.GetApi(g.GetProcAddress);
            wgl = WGL.GetApi();

            if (GVariables.OS == OS.Windows)
            {
                wglSwapIntervalEXT = Marshal.GetDelegateForFunctionPointer<APIEntryPoint>(wgl.GetProcAddress("wglSwapIntervalEXT"));
            }
            else
            {
                wglSwapIntervalEXT = (n) => { return false; };
            }
            wglSwapIntervalEXT.Invoke(0); // Disable VSync
            gl.Viewport(0, 0, (uint)GVariables.MainWindow.Size.X, (uint)GVariables.MainWindow.Size.Y);
            gl.ClearColor(0.5f, 0.5f, 0.5f, 1);


            string vendor = PTRGetString(gl.GetString(StringName.Vendor));
            string renderer = PTRGetString(gl.GetString(StringName.Renderer));

            Console.WriteLine($"Graphics Card Vendor: {vendor}");
            Console.WriteLine($"Graphics Card Model: {renderer}");
            SaturnEngine.Platform.Global.SetGPU(vendor);


        }
        uint pco = 0;

        public override void RenderFrame(double deltaTime)
        {
            gl.Clear(ClearBufferMask.ColorBufferBit);

            gl.BindTexture(TextureTarget.Texture2D, ttu);
            gl.BindVertexArray(vao);
            gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);


            gl.Flush();

            g.SwapBuffers(w);
        }

        public override void SetPosition(int x, int y)
        {

        }

        public override void SetScene(int index)
        {
            //throw new NotImplementedException();
        }

        public override void SetSize(int width, int height)
        {
            gl.Viewport(0, 0, (uint)width, (uint)height);
        }


        public override void Close()
        {
            gl.DeleteVertexArray(vao);
            gl.DeleteBuffer(vbo);
            gl.DeleteProgram(sp);
        }

        public override bool CheckSupport(Feature f)
        {
            throw new NotImplementedException();
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
                    //OpenGL不支持HDR
                    break;
                case Feature.DolbyVision:
                    //OpenGL不支持Dolby Vision
                    break;
            }
        }

        public override void SetUIScene(bool enable)
        {
            throw new NotImplementedException();
        }
    }
}
