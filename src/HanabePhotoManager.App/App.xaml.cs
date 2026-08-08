namespace HanabePhotoManager.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "HanabePhotoManager.SingleInstance";
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "Hanabe Photo Manager 已在运行。请关闭现有窗口后再启动。",
                "Hanabe Photo Manager",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        Services.ThemeManager.LoadAndApply();
        base.OnStartup(e);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        if (_ownsSingleInstanceMutex)
            _singleInstanceMutex?.ReleaseMutex();

        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        _ownsSingleInstanceMutex = false;
        base.OnExit(e);
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
