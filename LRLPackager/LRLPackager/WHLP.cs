using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using LRLPackager.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LRLPackager
{
    public static class ARGpool
    {
        public static bool OK =  false;
        public static Dictionary<string,object?> ARG = new Dictionary<string,object?>();
    }
    public interface IDialogService
    {
        Task<T?> ShowDialogAsync<T>(string viewName, object? viewModel = null);
    }
    public class DialogService : IDialogService
    {

        public DialogService()
        {

        }

        public async Task<T?> ShowDialogAsync<T>(string viewName, object? viewModel = null)
        {
            var view = CreateView(viewName);
            if (view == null) return default;

            if (viewModel != null)
            {
                view.DataContext = viewModel;
            }

            if (view is Window window)
            {
                var result = await window.ShowDialog<T>(GetTopLevelWindow());
                return result;
            }

            return default;
        }

        public static Window? GetTopLevelWindow()
        {
            return TopLevel.GetTopLevel((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow) as Window;
        }

        private Control? CreateView(string viewName)
        {
            var viewType = Type.GetType($"LRLPackager.Views.{viewName}");
            return viewType != null ? Activator.CreateInstance(viewType) as Control : null;
        }
    }
}
