using Avalonia.Controls;
using Avalonia.Interactivity;
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.Performance;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace CrossPlatformScriptRunner.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }
        private void LoadedThis(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("com.lofusoft.CPSR MainViewModel has been loaded.");
            Console.WriteLine("com.lofusoft.CPSR MainViewModel has been loaded.");
            Trace.WriteLine("com.lofusoft.CPSR MainViewModel has been loaded.");
            Dispatcher.Init();
            SELogger.Log("启用无线调试");

            SENLTcpHostConfig hc = new SENLTcpHostConfig();
            hc.HostName = "DebuggerHost";
            hc.ListenIp = IPAddress.Any;
            hc.ListenPort = 52525;

            SENLDebuggerFunctionConfig fc = new SENLDebuggerFunctionConfig();
            fc.FunctionList = new List<KeyValuePair<string, Action<string[]>>>();
            fc.FunctionList.Add(new KeyValuePair<string, Action<string[]>>("TestFunc", (args) => { SELogger.Log("TestFunc called with args: " + string.Join(", ", args)); }));
            fc.FunctionList.Add(new KeyValuePair<string, Action<string[]>>("Close", (args) => { SELogger.Log("This Window Will Be Closed"); GVariables.MainWindow.Close(); }));
            SENetLogger.Register(SENLHostType.TCP, SENLTcpMethod.Host, hc, fc);
            SELogger.Log("无线调试已启用，正在监听端口 " + hc.ListenPort);
            SELogger.Log("加载脚本引擎");
            //SaturnEngine.Platform.Global.EngineInit();
            GVariables.ScriptEngineGlobal = new SaturnEngine.ScriptEngine.SEScriptEngine();
            GVariables.ScriptEngineGlobal.Init(SaturnEngine.ScriptEngine.SEScriptEngine.EnableScriptType.All);
            SELogger.Log("加载完成");
            Debug.WriteLine("com.lofusoft.CPSR Engine Loaded");
            Console.WriteLine("com.lofusoft.CPSR Engine Loaded");
            Trace.WriteLine("com.lofusoft.CPSR Engine Loaded");
        }
        private void ThisUserControl_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 250)
            {
                CurrentFileComboBox.Width = e.NewSize.Width - 200;
                Debug.WriteLine($"UserControl size changed: New Width = {e.NewSize.Width}, CurrentFileComboBox Width set to {CurrentFileComboBox.Width}");
            }
            else 
            {
                CurrentFileComboBox.Width = 50;
                Debug.WriteLine($"UserControl size changed: New Width = {e.NewSize.Width}, CurrentFileComboBox Width set to {CurrentFileComboBox.Width}");
            }
            if(e.NewSize.Width > 300)
            {
                FileEditArea.Width = e.NewSize.Width;
            }
            else
            {
                FileEditArea.Width = 300;
            }
            if (e.NewSize.Height > 100)
            {
                FileEditArea.Height = e.NewSize.Height - 30;

            }
            else
            {
                FileEditArea.Height = 100;
            }
        }
    }
}