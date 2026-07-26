using Avalonia;
using HanabePhotoManager.Desktop.Core.ViewModels;

namespace HanabePhotoManager.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(argument => string.Equals(argument, "--smoke-test", StringComparison.Ordinal)))
        {
            DesktopStartupComposition.ValidateShell();
            _ = BuildAvaloniaApp();
            return 0;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
