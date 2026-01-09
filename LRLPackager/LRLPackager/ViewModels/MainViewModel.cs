using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SaturnEngine.Asset;
using SaturnEngine.Security;
using SaturnEngine.SEMath;

namespace LRLPackager.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _inlineFileName = "EMPTY";
        [ObservableProperty]
        private string _inlineFileTime = "EMPTY";
        [ObservableProperty]
        private string _inlineFileSize = "EMPTY";
        [ObservableProperty]
        private string _inlineFileType = "EMPTY";
        [ObservableProperty]
        private string _inlineFileAttr = "EMPTY";
        [ObservableProperty]
        private bool _inlineFileEnc = false;
        [ObservableProperty]
        private bool _inlineFileCom = false;
        [ObservableProperty]
        public ICommand _clickBTNCommand;

        public LRL? l;
        public MainViewModel()
        {
            Items = new ObservableCollection<string>
            {
                
            };

            _clickBTNCommand = new AsyncRelayCommand<string>(Clicked_BTN, (arg) => !string.IsNullOrEmpty(arg));
        }

        public void OnUnLoaded()
        {
            if (l != null)
            {
                l.Save();
                l.Close();
            }
        }
        
        private ObservableCollection<string> _items;
        private string _selectedItem;
        int _selectedIndex;
        public ObservableCollection<string> Items
        {
            get => _items;
            set => _items = value;
        }

        void LoadStFromLRL()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Items.Clear();
                if (l != null)
                {
                    for (int i = 0; i < l.BKs.Length; i++)
                    {
                        string nm = "";
                        for (int o = 0; o < l.BKs[i].ExtDataCount; o++)
                        {
                            if (l.BKs[i].Exts[o].t == LRL.LRBKExtDataType.Ext_BoxNameString)
                            {
                                nm = Encoding.UTF8.GetString(l.BKs[i].Exts[o].dt);
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(nm))
                            nm = l.BKs[i].NameSTC.ToString();
                        Items.Add(nm);
                    }
                }
            });
        }
        public string SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem=value;
                OnSelectedItemChanged();
            }
        }
        public int SelectedItemIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex=value;
                //OnSelectedItemChanged();
            }
        }
        private void OnSelectedItemChanged()
        {
            Console.WriteLine($"当前选中: {SelectedItem}");
            Debug.WriteLine($"当前选中: {SelectedItem}");
            // 执行其他逻辑

            if (l != null)
            {
                var bk =  l.BKs[SelectedItemIndex];
                InlineFileEnc = bk.Encrypt;
                InlineFileCom = LRL.HasFlag(bk.flg,LRL.LRBKFlag.Compress);
                InlineFileAttr = bk.flg.ToString();
                InlineFileName = SelectedItem;
                InlineFileTime = l.ti.GetDateTime().ToString();
                InlineFileType = l.FLG.ToString();
                InlineFileSize = bk.data.Length.ToString();
            }
        }
        
        public async Task Clicked_BTN(string? arg)
        {
            Debug.WriteLine($"Called Function With Arg:{arg}");
            if ((arg == null))
            {
                return;
            }
            await Task.Delay(10);
            switch (arg)
            {
                case "F_O"://打开文件
                    if (l != null)
                    {
                        l.Save();
                        l.Close();
                    }

                    var pk = new FilePickerOpenOptions();
                    pk.Title = "选择LRL文件";
                    pk.AllowMultiple = false;
                    pk.FileTypeFilter = [new FilePickerFileType(".lrl")];
                    var w = DialogService.GetTopLevelWindow();
                    if (w != null)
                    {
                        var f = await w.StorageProvider.OpenFilePickerAsync(pk);
                        if (f.Count > 0)
                        {
                            l = new LRL();
                            l.LoadFromFile(Uri.UnescapeDataString(f[0].Path.AbsolutePath));
                            LoadStFromLRL();
                        }
                    }
                    break;
                case "F_N"://新建文件
                    var pk22 = new FilePickerSaveOptions();
                    pk22.Title = "选择保存位置";
                    //pk22.DefaultExtension = ".lrl";
                    //pk22.SuggestedFileName = Path.GetFileName("UnknownName");
                    pk22.FileTypeChoices = [new FilePickerFileType(".lrl")];
                    var w22 = DialogService.GetTopLevelWindow();
                    if (w22 != null)
                    {
                        var v = await w22.StorageProvider.SaveFilePickerAsync(pk22);
                        if (v != null)
                        {
                            
                            l =  new LRL();
                            l.CreateNewFile(Uri.UnescapeDataString(v.Path.AbsolutePath));
                            l.FLG = LRL.LRLFlag.Allow_All;
                            l.Save();
                            LoadStFromLRL();
                        }
                    }
                    break;
                case "F_S"://保存文件
                    if(l != null)
                        l.Save();
                    break;
                case "F_SA"://另存为
                    if (l != null)
                    {
                        var pk2 = new FilePickerSaveOptions();
                        pk2.Title = "选择保存位置";
                        pk2.DefaultExtension = ".lrl";
                        pk2.SuggestedFileName = Path.GetFileName(l.FP??"UnknownName");
                        var w2 = DialogService.GetTopLevelWindow();
                        if (w2 != null)
                        {
                            var v = await w2.StorageProvider.SaveFilePickerAsync(pk2);
                            if (v != null)
                            {
                                string p = l.FP;
                                l.Save();
                                l.Close();
                                File.Copy(p, Uri.UnescapeDataString(v.Path.AbsolutePath));
                                l =  new LRL();
                                l.LoadFromFile(Uri.UnescapeDataString(v.Path.AbsolutePath));
                            }
                        }
                    }
                    break;
                case "O_A"://添加文件
                    if (l != null)
                    {
                        IDialogService id = new DialogService();
                        await id.ShowDialogAsync<object>("AddFileView");
                        //AvaloniaXamlLoader.Load("./AddFileView.axaml");
                        if (ARGpool.OK)
                        {
                            //处理 文件
                            var fd = File.Open((string)(ARGpool.ARG["FP"]), FileMode.Open);
                            string? nm = (string?)ARGpool.ARG["NM"];
                            bool jm = (bool)ARGpool.ARG["JM"];
                            bool ys = (bool)ARGpool.ARG["YS"];
                            string ps =  (string)ARGpool.ARG["PS"];
                            LRL.LRBKFlag f = LRL.LRBKFlag.None;
                            if (ys)
                                f |= LRL.LRBKFlag.Compress;
                            if (jm)
                            {
                                ulong c = ps.ToSTC();
                                ulong cc = c.ToSTC();
                                Helper.DataLayout dl = new Helper.DataLayout();
                                dl.UL = cc;
                                KeyValuePair<LRL.LRBKExtDataType, byte[]>[] fgs =
                                [
                                    new KeyValuePair<LRL.LRBKExtDataType, byte[]>(LRL.LRBKExtDataType.Ext_Encrypt, dl.GetBytes())
                                ];
                                l.AddBox(fd, nm: nm, leaveclose: true, bf: f, passwordstc: c, extdata: fgs);
                            }
                            else
                            {
                                l.AddBox(fd, nm: nm, leaveclose: true, bf: f);

                            }
                            LoadStFromLRL();
                        }
                    }
                    break;
                case "O_D"://删除所选文件
                    if (l != null)
                    {
                        l.RemoveBox(SelectedItemIndex);
                        LoadStFromLRL();
                    }
                    break;
                case "O_E"://导出所选文件
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
