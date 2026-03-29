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
        GVariables.Developer = "Losment";
        GVariables.ProgramName = "Saturn Engine";
        GVariables.AllowUseWinHook = false;
        GVariables.CurrentWindowHostType = WindowHostType.SDL;
        SELogger.Log("自定义初始化结束", "CS SCRIPT");
    }
}
