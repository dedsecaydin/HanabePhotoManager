using System.Windows;

namespace HanabePhotoManager.InstallerShell;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Any(argument => string.Equals(argument, "/quiet", StringComparison.OrdinalIgnoreCase)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var uninstall = e.Args.Any(argument => string.Equals(argument, "/uninstall", StringComparison.OrdinalIgnoreCase));
            var logIndex = Array.FindIndex(e.Args, argument => string.Equals(argument, "/log", StringComparison.OrdinalIgnoreCase));
            var logPath = logIndex >= 0 && logIndex + 1 < e.Args.Length ? e.Args[logIndex + 1] : null;
            try
            {
                var engine = new InstallerEngine();
                var msiPath = engine.ExtractEmbeddedMsi();
                var exitCode = await engine.RunQuietAsync(msiPath, uninstall, logPath, CancellationToken.None);
                Shutdown(exitCode);
            }
            catch
            {
                Shutdown(1603);
            }
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
