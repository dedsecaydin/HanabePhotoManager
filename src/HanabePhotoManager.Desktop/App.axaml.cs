using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using HanabePhotoManager.Desktop.Composition;
using HanabePhotoManager.Desktop.Core.ViewModels;
using HanabePhotoManager.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace HanabePhotoManager.Desktop;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _serviceProvider = new ServiceCollection()
                .AddHanabeDesktop()
                .BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<DesktopShellViewModel>()
            };
            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }
}
