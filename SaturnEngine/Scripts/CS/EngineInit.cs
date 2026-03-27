using SaturnEngine.Global;
using SaturnEngine.Management;
using System;
using System.Collections.Generic;
using System.Text;

public class Program
{
    public static void Main()
    {
        SELogger.Log("自定义初始化开始", "CS SCRIPT");
        //当前配置
        GVariables.ProgramType = ProgramTypes.Game2D;
        GVariables.CurrentWindowHostType = WindowHostType.SDL;
        
        //Glfw
        //GVariables.CurrentWindowHostType = WindowHostType.Glfw;
        if (OperatingSystem.IsWindows())
        {
            GVariables.OS = OS.Windows;
            if (GVariables.OSVersion.Major <= 7)
            {
                throw new PlatformNotSupportedException();
            }
            //DX default
            //GVariables.GraphicsAPI = GraphicsAPI.OpenGL2D;
            //SELogger.Log("" + GVariables.GraphicsAPI);
            GVariables.GraphicsAPI = GraphicsAPI.Vulkan;
            GVariables.GraphicsBaseLevel = new Version(1, 1);
            GVariables.GraphicsAimLevel = new Version(1, 4);
        }
        else if (OperatingSystem.IsLinux())
        {
            GVariables.OS = OS.Linux;

            GVariables.GraphicsAPI = GraphicsAPI.Vulkan;
            GVariables.GraphicsBaseLevel = new Version(1, 1);
            GVariables.GraphicsAimLevel = new Version(1, 4);
        }
        else if (OperatingSystem.IsAndroid())
        {
            GVariables.OS = OS.Android;

            GVariables.GraphicsAPI = GraphicsAPI.Vulkan;
            GVariables.GraphicsBaseLevel = new Version(1, 1);
            GVariables.GraphicsAimLevel = new Version(1, 3);
        }
        else if (OperatingSystem.IsIOS())
        {
            GVariables.OS = OS.IOS;

            GVariables.GraphicsAPI = GraphicsAPI.Vulkan;
            GVariables.GraphicsBaseLevel = new Version(1, 1);
            GVariables.GraphicsAimLevel = new Version(1, 3);
        }
        else if (OperatingSystem.IsMacOS())
        {
            GVariables.OS = OS.MacOS;

            GVariables.GraphicsAPI = GraphicsAPI.SDL2D;
            GVariables.GraphicsBaseLevel = new Version(1, 1);
            GVariables.GraphicsAimLevel = new Version(1, 4);

        }
        else
        {
#if HARMONYOS
                GVariables.OS = OS.HarmonyOS;
#elif HYPEROS
                GVariables.OS = OS.HyperOS;
#elif XBOX
                GVariables.OS = OS.XBox;
#elif PS
                GVariables.OS = OS.PlayStation; 
#else
            GVariables.OS = OS.Unknow;
#endif
            GVariables.GraphicsAPI = GraphicsAPI.Vulkan;
            GVariables.GraphicsBaseLevel = new Version(1, 1);
            GVariables.GraphicsAimLevel = new Version(1, 3);

            SELogger.Log("未识别到系统或者net无法处理的系统", "CS SCRIPT");
            //just for testing
            GVariables.GraphicsAPI = GraphicsAPI.SDL2D;
            GVariables.GraphicsBaseLevel = new Version(3, 3);
            GVariables.GraphicsAimLevel = new Version(4, 0);
        }
        GVariables.Developer = "Losment";
        GVariables.ProgramName = "Saturn Engine";
        GVariables.AllowUseWinHook = false;
        GVariables.GraphicsAPI = GraphicsAPI.SDL2D;
        GVariables.CurrentWindowHostType = WindowHostType.SDL;
        SELogger.Log("自定义初始化结束", "CS SCRIPT");
    }
}
