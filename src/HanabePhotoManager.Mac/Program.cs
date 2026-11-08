using Avalonia;
using Avalonia.Media;

namespace HanabePhotoManager.Mac;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .With(new MacOSPlatformOptions { ShowInDock = true })
        .With(new FontManagerOptions
        {
            // macOS 用苹方，Windows 调试时用雅黑。
            DefaultFamilyName = OperatingSystem.IsMacOS() ? "PingFang SC" : "Microsoft YaHei UI",
        })
        .LogToTrace();
}
