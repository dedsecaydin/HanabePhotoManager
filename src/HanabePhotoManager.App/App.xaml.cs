namespace HanabePhotoManager.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "HanabePhotoManager.SingleInstance";
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    /// <summary>
    /// When the app is launched with <c>--screenshot &lt;path&gt;</c>, the main
    /// window renders itself to a PNG at that path (headless-safe) and exits.
    /// Used by the milestone screenshot workflow instead of PrintWindow, which
    /// returns blank in headless/remote desktop sessions.
    /// </summary>
    internal static string? ScreenshotPath { get; private set; }

    /// <summary>
    /// When set (with <c>--page &lt;Name&gt;</c>), the main window navigates to
    /// this page before the screenshot is rendered. Used to capture non-default
    /// pages (e.g. Home) in the headless-safe screenshot workflow.
    /// </summary>
    internal static string? ScreenshotPage { get; private set; }

    /// <summary>
    /// When set (with <c>--select-first</c>), the main window selects the first
    /// library photo before rendering the screenshot. Used to capture the
    /// contextual Inspector panel in the headless-safe screenshot workflow.
    /// </summary>
    internal static bool SelectFirstForScreenshot { get; private set; }

    /// <summary>
    /// When set (with <c>--browse-showcase</c>), the browse page is switched to the
    /// grid display mode and the browse-condition filter chips are expanded before
    /// the screenshot is rendered. Used to capture the M3 variant-001 browse layout
    /// (workspace grid + filter chips + 320px inspector + FAB) in one pass.
    /// </summary>
    internal static bool BrowseShowcaseForScreenshot { get; private set; }

    /// <summary>
    /// When set (with <c>--advanced-filters</c>), the browse page's advanced filter
    /// section is expanded before the screenshot is rendered. Used to capture the
    /// expanded state of the collapsed advanced-filter section.
    /// </summary>
    internal static bool AdvancedFiltersForScreenshot { get; private set; }

    /// <summary>
    /// When set (with <c>--select-first-person</c>), the people page selects the
    /// first person album before the screenshot is rendered, so the person detail
    /// (hero + merge entry + virtualized photo grid) is captured instead of the
    /// avatar-grid overview.
    /// </summary>
    internal static bool SelectFirstPersonForScreenshot { get; private set; }

    /// <summary>
    /// When set (with <c>--cloud-provider &lt;baidu|quark&gt;</c>), the cloud page
    /// selects the given provider tab before the screenshot is rendered. Used to
    /// capture each provider's real account overview.
    /// </summary>
    internal static string? ScreenshotCloudProvider { get; private set; }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        var args = e.Args;
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--screenshot", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                ScreenshotPath = args[index + 1];
                index++;
            }
            else if (string.Equals(arg, "--page", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                ScreenshotPage = args[index + 1];
                index++;
            }
            else if (string.Equals(arg, "--select-first", StringComparison.OrdinalIgnoreCase))
            {
                SelectFirstForScreenshot = true;
            }
            else if (string.Equals(arg, "--browse-showcase", StringComparison.OrdinalIgnoreCase))
            {
                BrowseShowcaseForScreenshot = true;
            }
            else if (string.Equals(arg, "--advanced-filters", StringComparison.OrdinalIgnoreCase))
            {
                AdvancedFiltersForScreenshot = true;
            }
            else if (string.Equals(arg, "--select-first-person", StringComparison.OrdinalIgnoreCase))
            {
                SelectFirstPersonForScreenshot = true;
            }
            else if (string.Equals(arg, "--cloud-provider", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                ScreenshotCloudProvider = args[index + 1];
                index++;
            }
        }

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
