using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LRLPackager.ViewModels;

namespace LRLPackager.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            
        }

        private void Control_OnUnloaded(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Unloaded");
            (DataContext as MainViewModel).OnUnLoaded();
        }
    }
}