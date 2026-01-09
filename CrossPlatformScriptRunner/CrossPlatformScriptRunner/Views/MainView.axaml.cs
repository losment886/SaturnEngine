using Avalonia.Controls;
using Avalonia.Interactivity;
using SaturnEngine.Global;
using System;
using System.Diagnostics;

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
            //SaturnEngine.Platform.Global.EngineInit();
            GVariables.ScriptEngineGlobal = new SaturnEngine.ScriptEngine.SEScriptEngine();
            GVariables.ScriptEngineGlobal.Init(SaturnEngine.ScriptEngine.SEScriptEngine.EnableScriptType.All);
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