namespace HanabePhotoManager.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        Services.ThemeManager.LoadAndApply();
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogUnhandledException("UI", e.Exception);
        System.Windows.MessageBox.Show(
            "发生了未处理的错误。详细信息已记录；请保存工作后重启应用。",
            "Hanabe Photo Manager",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        LogUnhandledException("AppDomain", e.ExceptionObject);
    }

    private static void LogUnhandledException(string source, object exception)
    {
        var message = $"{DateTimeOffset.Now:O} [{source}] {exception}{Environment.NewLine}";
        System.Diagnostics.Trace.TraceError(message);
        try
        {
            var logDirectory = System.IO.Path.Combine(Services.AppDataPaths.Root, "Logs");
            System.IO.Directory.CreateDirectory(logDirectory);
            System.IO.File.AppendAllText(System.IO.Path.Combine(logDirectory, "unhandled-exceptions.log"), message);
        }
        catch
        {
            // Logging must never turn a recoverable UI exception into a second failure.
        }
    }
}
