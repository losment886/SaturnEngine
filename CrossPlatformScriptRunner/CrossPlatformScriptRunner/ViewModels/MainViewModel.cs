using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using SaturnEngine.Global;
using System.Diagnostics;
using System.Linq;

namespace CrossPlatformScriptRunner.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _currentFileName = "No file loaded.";
        [ObservableProperty]
        private ComboBoxItem[] _currentFiles = [];
        [ObservableProperty]
        private string _fileEditAreaText = "";

        public UserControl ItSelf;
        
        void OpenFile()
        {
            // Implementation for opening a file
            
            var tl = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if(tl != null)
            {
                var f = tl.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Open Script File",
                    AllowMultiple = false,
                    FileTypeFilter = new System.Collections.Generic.List<Avalonia.Platform.Storage.FilePickerFileType>
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Script Files")
                    {
                        Patterns = new System.Collections.Generic.List<string> { "*.txt", "*.script", "*.cs", "*.js" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("All Files")
                    {
                        Patterns = new System.Collections.Generic.List<string> { "*.*" }
                    }
                }
                }).Result;
                if (f != null)
                {
                    foreach (var file in f)
                    {
                        CurrentFileName = file.Name;
                        using (var stream = file.OpenReadAsync().Result)
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            string v = reader.ReadToEnd();
                            FileEditAreaText = v;
                            ComboBoxItem ci = new ComboBoxItem();
                            ci.Content = CurrentFileName;
                            CurrentFiles = CurrentFiles.Append(ci).ToArray();
                        }
                        Debug.WriteLine($"Opened file: {file.Name}");
                    }
                }
            }

        }
        void RunFile()
        {
            // Implementation for running a file
            string t = FileEditAreaText;
            if (t != null && !string.IsNullOrEmpty(t)) 
            {
                var id = GVariables.ScriptEngineGlobal.CompileCode(t, SaturnEngine.ScriptEngine.SEScriptEngine.ScriptType.CSharp);
                object? j = GVariables.ScriptEngineGlobal.RunMain(id);
                Debug.WriteLine(j);
                ComboBoxItem ci = new ComboBoxItem();
                ci.Content = j;
                CurrentFiles = CurrentFiles.Append(ci).ToArray();
            }
        }
        public void Clicked_BTN(string arg)
        {
            Debug.WriteLine($"Button clicked with argument: {arg}");
            switch(arg)
            {
                case "F_O"://打开文件
                    OpenFile();
                    break;
                case "F_N"://新建文件
                    break;
                case "F_S"://保存文件
                    break;
                case "F_SA"://另存为
                    break;
                case "R_B"://编译脚本
                    break;
                case "R_BA"://编译全部脚本
                    break;
                case "R_R"://运行脚本
                    RunFile();
                    break;
                case "R_RA"://运行全部脚本
                    break;
                case "H_W"://显示帮助窗口
                    break;
                case "H_U"://更新
                    break;
                case "H_F"://反馈
                    break;
                case "H_O"://设置
                    break;
                case "H_A"://关于
                    break;
                default:
                    Debug.WriteLine("Unknown button argument.");
                    break;
            }
        }
    }
}
