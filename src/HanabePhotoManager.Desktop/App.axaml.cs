using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using HanabePhotoManager.Desktop.Core.ViewModels;
using HanabePhotoManager.Desktop.Views;

namespace HanabePhotoManager.Desktop;

public partial class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new DesktopShellViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
