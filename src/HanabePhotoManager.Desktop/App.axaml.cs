using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using HanabePhotoManager.Desktop.Composition;
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
            _serviceProvider = DesktopComposition.CreateServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = DesktopComposition.ResolveServicesForCurrentPlatform(_serviceProvider)
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
