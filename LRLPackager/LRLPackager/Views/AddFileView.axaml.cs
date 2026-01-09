using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LRLPackager.ViewModels;

namespace LRLPackager.Views;

public partial class AddFileView : Window
{
    private string fp;
    public AddFileView()
    {
        InitializeComponent();
        DataContext = new AddFileViewModel();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Ld();
    }

    async Task Ld()
    {
        var fpk = new FilePickerOpenOptions();
        var w = this;
        var vl = await w.StorageProvider.OpenFilePickerAsync(fpk);
        if (vl.Count > 0)
        {
            fp = Uri.UnescapeDataString(vl[0].Path.AbsolutePath);
            Dispatcher.UIThread.Invoke(() =>
            {
                NameBox.Text = vl[0].Name;
            });
        }
        else
        {
            Close();
        }
    }
    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ARGpool.OK = false;
        this.Close();
    }

    private void EnterButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ARGpool.OK = true;
        //string g = $"{CheckerC.IsChecked}";
        ARGpool.ARG = new Dictionary<string, object?>();
        ARGpool.ARG.Add("JM", CheckerE.IsChecked);
        ARGpool.ARG.Add("YS", CheckerC.IsChecked);
        ARGpool.ARG.Add("NM", NameBox.Text);
        ARGpool.ARG.Add("FP", fp);
        ARGpool.ARG.Add("PS", PassBox.Text);
        this.Close();   
    }
    
}