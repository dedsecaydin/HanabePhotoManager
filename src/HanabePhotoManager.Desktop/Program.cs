using Avalonia;
using Avalonia.Themes.Fluent;
using HanabePhotoManager.Desktop.Composition;
using HanabePhotoManager.Desktop.Core.ViewModels;
using HanabePhotoManager.Desktop.Views;

namespace HanabePhotoManager.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(argument => string.Equals(argument, "--smoke-test", StringComparison.Ordinal)))
        {
            return RunSmokeTest();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    internal static int RunSmokeTest()
    {
        DesktopStartupComposition.ValidateShell();
        using var serviceProvider = DesktopComposition.CreateServiceProvider();
        var shellViewModel = DesktopComposition.ResolveServicesForCurrentPlatform(serviceProvider);
        BuildAvaloniaApp().SetupWithoutStarting();

        if (Application.Current is not App app || !app.Styles.OfType<FluentTheme>().Any())
        {
            throw new InvalidOperationException("The Avalonia application and Fluent theme must load at startup.");
        }

        var mainWindow = new MainWindow
        {
            DataContext = shellViewModel
        };

        try
        {
            if (mainWindow.Content is null || mainWindow.IsVisible)
            {
                throw new InvalidOperationException("The main window XAML must load without showing the window.");
            }

            return 0;
        }
        finally
        {
            mainWindow.Close();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
