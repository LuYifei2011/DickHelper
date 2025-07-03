using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using DickHelper.ViewModels;
using DickHelper.Views;
using System;
using System.Linq;

namespace DickHelper;

public partial class App : Application
{
    public static HistoryViewModel HistoryViewModelInstance => HistoryViewModel.Instance;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 桌面端用 MainWindow（含原有 TabControl）
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            // 安卓端用 DialogHost 包裹 TabHostView，确保 DialogHost 可用
            var tabHostView = new Views.TabHostView
            {
                DataContext = new MainViewModel()
            };
            var dialogHostType = Type.GetType("DialogHostAvalonia.DialogHost, DialogHost.Avalonia");
            if (dialogHostType != null)
            {
                var dialogHost = (Avalonia.Controls.ContentControl)Activator.CreateInstance(dialogHostType)!;
                dialogHost.SetValue(Avalonia.Controls.ContentControl.ContentProperty, tabHostView);
                singleViewPlatform.MainView = dialogHost;
            }
            else
            {
                // DialogHost 类型未找到，回退为原始视图
                singleViewPlatform.MainView = tabHostView;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}