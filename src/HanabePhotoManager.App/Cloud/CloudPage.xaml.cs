using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.Core.Cloud;
using HanabePhotoManager.Infrastructure.Cloud;
using Microsoft.Web.WebView2.Core;

namespace HanabePhotoManager.App.Cloud;

public partial class CloudPage : System.Windows.Controls.UserControl, IDisposable
{
    public static readonly DependencyProperty InitialUrlProperty =
        DependencyProperty.Register(nameof(InitialUrl), typeof(string), typeof(CloudPage),
            new PropertyMetadata("https://pan.baidu.com"));
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(CloudPage),
            new PropertyMetadata(false, OnIsActiveChanged));
    public static readonly DependencyProperty IsDarkThemeProperty =
        DependencyProperty.Register(nameof(IsDarkTheme), typeof(bool), typeof(CloudPage),
            new PropertyMetadata(false, OnIsDarkThemeChanged));

    private const string DarkThemeBridgeScript = """
        (() => {
          const styleId = "hanabe-cloud-dark-style";
          const darkCss = `
            :root { color-scheme: dark !important; background-color: #121416 !important; }
            html, body { background-color: #121416 !important; color: #e8eaed !important; }
            input, textarea, select, button {
              background-color: #24282c !important;
              color: #e8eaed !important;
              border-color: #495057 !important;
            }
            header, nav, aside, main, section, article,
            [class*="header"], [class*="nav"], [class*="panel"], [class*="dialog"], [class*="modal"] {
              border-color: #3a3f44 !important;
            }
            img, picture, video, canvas, svg, iframe, [role="img"],
            [class*="captcha"], [id*="captcha"], [class*="verify"], [id*="verify"],
            [class*="qrcode"], [id*="qrcode"], [class*="qr-code"], [id*="qr-code"] {
              isolation: auto !important;
            }`;
          window.__hanabeApplyCloudTheme = dark => {
            const existing = document.getElementById(styleId);
            if (!dark) {
              existing?.remove();
              document.documentElement.removeAttribute("data-hanabe-cloud-theme");
              return;
            }
            const style = existing || document.createElement("style");
            style.id = styleId;
            style.textContent = darkCss;
            if (!existing) (document.head || document.documentElement).appendChild(style);
            document.documentElement.setAttribute("data-hanabe-cloud-theme", "dark");
          };
          const preference = window.matchMedia("(prefers-color-scheme: dark)");
          preference.addEventListener?.("change", event => window.__hanabeApplyCloudTheme(event.matches));
          window.__hanabeApplyCloudTheme(preference.matches);
        })();
        """;

    private bool _browserInitialized;
    private bool _hasBeenVisible;
    private bool _disposed;
    private CancellationTokenSource? _navigationTimeout;
    private readonly SemaphoreSlim _themeGate = new(1, 1);
    private string? _themeScriptId;
    private int _themeVersion;
    private CoreWebView2Environment? _environment;
    private CloudHubViewModel? _viewModel;

    public CloudPage()
    {
        InitializeComponent();
        IsDarkTheme = ThemeManager.Current == AppTheme.Dark;
        ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
    }

    public string InitialUrl
    {
        get => (string)GetValue(InitialUrlProperty);
        set => SetValue(InitialUrlProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsDarkTheme
    {
        get => (bool)GetValue(IsDarkThemeProperty);
        set => SetValue(IsDarkThemeProperty, value);
    }

    private static void OnIsActiveChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CloudPage page)
            _ = page.ApplyActiveStateAsync((bool)e.NewValue);
    }

    private static void OnIsDarkThemeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not CloudPage page) return;
        var version = Interlocked.Increment(ref page._themeVersion);
        _ = page.ApplyThemeAsync(version);
    }

    private void ThemeManager_ThemeChanged(object? sender, AppTheme theme)
    {
        if (_disposed) return;
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (!_disposed)
                IsDarkTheme = theme == AppTheme.Dark;
        });
    }

    private async Task ApplyActiveStateAsync(bool isActive)
    {
        if (_disposed) return;
        if (isActive)
        {
            if (!_hasBeenVisible)
            {
                _hasBeenVisible = true;
                await InitializeBrowserAsync();
                await InitializeCloudOverviewAsync();
            }
            else
            {
                CloudLoginBrowser.CoreWebView2?.Resume();
                await ApplyThemeAsync(Interlocked.Increment(ref _themeVersion));
                if (_viewModel is not null)
                {
                    // 百度 ↔ 夸克 切换（或重新进入网盘页）时刷新当前账户状态。
                    await _viewModel.RefreshAsync();
                }
            }
        }
        else if (_browserInitialized && CloudLoginBrowser.CoreWebView2 is not null)
        {
            try
            {
                await CloudLoginBrowser.CoreWebView2.TrySuspendAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 suspend failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Lazily creates the page-scoped <see cref="CloudHubViewModel"/> (one per
    /// provider host: Baidu vs Quark) and binds the right-hand overview
    /// inspector to it. Provider selection reads the real encrypted session
    /// store, so a missing session yields an honest "not logged in" state.
    /// </summary>
    private async Task InitializeCloudOverviewAsync()
    {
        try
        {
            if (_viewModel is null)
            {
                _viewModel = await CreateCloudHubViewModelAsync();
                CloudOverviewInspector.DataContext = _viewModel;
            }

            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            ShowErrorState("云盘状态读取失败", ex.Message);
            System.Diagnostics.Debug.WriteLine($"Cloud overview init failed: {ex.Message}");
        }
    }

    private async Task<CloudHubViewModel> CreateCloudHubViewModelAsync()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HanabePhotoManager", "Cloud");
        return await CreateCloudHubViewModelAsync(root, IsQuarkHost, SynchronizationContext.Current);
    }

    /// <summary>
    /// Builds the real cloud hub stack for one provider host: encrypted session
    /// store, sqlite index, file cache and JSON transfer queue, plus the
    /// provider chosen from the persisted session (honest unauthenticated state
    /// when no data source exists). Exposed internally so tests can exercise the
    /// exact production wiring against a temporary data root.
    /// </summary>
    internal static async Task<CloudHubViewModel> CreateCloudHubViewModelAsync(
        string dataRoot,
        bool isQuark,
        SynchronizationContext? synchronizationContext = null)
    {
        Directory.CreateDirectory(dataRoot);
        var sessions = new EncryptedCloudSessionStore(Path.Combine(dataRoot, "sessions.dat"));
        var index = new SqliteCloudIndexStore(Path.Combine(dataRoot, "cloud-index.db"));
        var cache = new FileCloudCacheStore(Path.Combine(dataRoot, "cache"), () => DateTimeOffset.UtcNow);
        var queue = new JsonCloudTransferQueueStore(Path.Combine(dataRoot, "transfers.json"));
        var provider = await CreateProviderAsync(sessions, isQuark);
        return new CloudHubViewModel(provider, index, cache, synchronizationContext, queue);
    }

    private static async Task<ICloudProvider> CreateProviderAsync(
        EncryptedCloudSessionStore sessions,
        bool isQuark)
    {
        if (isQuark)
        {
            // 夸克网盘连接器尚未实现：如实显示未接入，不伪造任何账户数据。
            return new UnauthenticatedCloudProvider(
                CloudProviderKind.Quark,
                "夸克网盘",
                "未接入 · 夸克网盘连接器尚未实现");
        }

        var token = await sessions.LoadAsync(CloudProviderKind.Baidu);
        if (token is null)
        {
            // 没有已保存的百度 API 会话：如实显示未登录，而不是伪造容量数据。
            return new UnauthenticatedCloudProvider(
                CloudProviderKind.Baidu,
                "百度网盘",
                "未登录 · 未找到已保存的 API 会话");
        }

        return new BaiduCloudProvider(
            new HttpClient(),
            () => LoadBaiduTokenAsync(sessions));
    }

    private static async Task<CloudAuthToken> LoadBaiduTokenAsync(EncryptedCloudSessionStore sessions) =>
        await sessions.LoadAsync(CloudProviderKind.Baidu)
        ?? throw new InvalidOperationException("百度网盘尚未登录，未找到已保存的 API 会话。");

    private bool IsQuarkHost => InitialUrl.Contains("quark", StringComparison.OrdinalIgnoreCase);

    private async Task InitializeBrowserAsync()
    {
        if (_browserInitialized || _disposed) return;
        _browserInitialized = true;
        ShowLoadingState("正在加载云服务", "正在准备安全的内嵌浏览器，请稍候。");

        try
        {
            await InitializeBrowserCoreAsync(useFallbackDirectory: false);
            await FinishBrowserSetupAsync();
        }
        catch (Exception ex)
        {
            // 0x8007139F (ERROR_INVALID_STATE)：UserDataFolder 常被未完全释放的前一实例/
            // 锁文件占用。换用独立唯一子目录重试一次，绕开被占用的目录。
            try
            {
                ShowLoadingState("正在加载云服务", "初始化目录被占用，正在改用独立目录重试…");
                await InitializeBrowserCoreAsync(useFallbackDirectory: true);
                await FinishBrowserSetupAsync();
            }
            catch (Exception retryEx)
            {
                _browserInitialized = false;
                ShowErrorState("云服务暂时无法打开", $"内嵌浏览器初始化失败：{retryEx.Message}");
                System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex.Message}; retry failed: {retryEx.Message}");
            }
        }
    }

    private async Task InitializeBrowserCoreAsync(bool useFallbackDirectory)
    {
        var userData = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HanabePhotoManager", "WebView2", SafeFolderName(InitialUrl));
        if (useFallbackDirectory)
        {
            // 独立唯一子目录：WebView2\Cloud\<host>\<进程ID>-<时间戳>
            userData = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HanabePhotoManager", "WebView2", "Cloud", SafeFolderName(InitialUrl),
                $"{Environment.ProcessId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        }

        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
        await CloudLoginBrowser.EnsureCoreWebView2Async(environment);
        _environment = environment;
    }

    private async Task FinishBrowserSetupAsync()
    {
        var browser = CloudLoginBrowser.CoreWebView2;
        browser.NavigationStarting -= CoreWebView2_NavigationStarting;
        browser.NavigationCompleted -= CoreWebView2_NavigationCompleted;
        browser.NavigationStarting += CoreWebView2_NavigationStarting;
        browser.NavigationCompleted += CoreWebView2_NavigationCompleted;
        browser.Settings.UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        await ApplyThemeAsync(Interlocked.Increment(ref _themeVersion));
        browser.Navigate(InitialUrl);
    }

    private async Task ApplyThemeAsync(int version)
    {
        if (_disposed || CloudLoginBrowser.CoreWebView2 is null) return;
        await _themeGate.WaitAsync();
        try
        {
            if (_disposed || version != Volatile.Read(ref _themeVersion)) return;
            var browser = CloudLoginBrowser.CoreWebView2;
            if (browser is null) return;

            browser.Profile.PreferredColorScheme = IsDarkTheme
                ? CoreWebView2PreferredColorScheme.Dark
                : CoreWebView2PreferredColorScheme.Light;
            _themeScriptId ??= await browser.AddScriptToExecuteOnDocumentCreatedAsync(DarkThemeBridgeScript);
            await browser.ExecuteScriptAsync(
                $"window.__hanabeApplyCloudTheme?.({(IsDarkTheme ? "true" : "false")});");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 theme sync failed: {ex.Message}");
        }
        finally
        {
            _themeGate.Release();
        }
    }

    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        ShowLoadingState("正在连接云服务", "正在加载登录页面，请稍候。");
        StartNavigationTimeout();
    }

    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        CancelNavigationTimeout();
        if (!e.IsSuccess)
        {
            ShowErrorState("云页面加载失败", $"无法完成加载（{e.WebErrorStatus}）。请检查网络后重试。");
            return;
        }
        if (CloudLoginBrowser.Source is null || CloudLoginBrowser.Source.ToString() == "about:blank")
        {
            ShowEmptyState("云页面暂无内容", "当前页面没有可显示的内容，请返回首页或重试。");
            return;
        }
        ShowContentState();
    }

    private void StartNavigationTimeout()
    {
        CancelNavigationTimeout();
        _navigationTimeout = new CancellationTokenSource();
        _ = ObserveNavigationTimeoutAsync(_navigationTimeout.Token);
    }

    private async Task ObserveNavigationTimeoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
                ShowErrorState("云页面响应超时", "页面长时间没有完成加载，请检查网络后重试。");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelNavigationTimeout()
    {
        _navigationTimeout?.Cancel();
        _navigationTimeout?.Dispose();
        _navigationTimeout = null;
    }

    private void ShowLoadingState(string title, string description)
    {
        CloudLoginBrowser.Visibility = Visibility.Collapsed;
        CloudStatusPanel.Visibility = Visibility.Visible;
        CloudStatusTitle.Text = title;
        CloudStatusDescription.Text = description;
        CloudRetryButton.Visibility = Visibility.Collapsed;
    }

    private void ShowContentState()
    {
        CloudStatusPanel.Visibility = Visibility.Collapsed;
        CloudLoginBrowser.Visibility = Visibility.Visible;
    }

    private void ShowEmptyState(string title, string description)
    {
        CloudLoginBrowser.Visibility = Visibility.Collapsed;
        CloudStatusPanel.Visibility = Visibility.Visible;
        CloudStatusTitle.Text = title;
        CloudStatusDescription.Text = description;
        CloudRetryButton.Visibility = Visibility.Visible;
    }

    private void ShowErrorState(string title, string description)
    {
        CloudLoginBrowser.Visibility = Visibility.Collapsed;
        CloudStatusPanel.Visibility = Visibility.Visible;
        CloudStatusTitle.Text = title;
        CloudStatusDescription.Text = description;
        CloudRetryButton.Visibility = Visibility.Visible;
    }

    private async void CloudRetry_Click(object sender, RoutedEventArgs e)
    {
        if (_disposed) return;
        if (CloudLoginBrowser.CoreWebView2 is not null)
        {
            CloudLoginBrowser.CoreWebView2.Navigate(InitialUrl);
            return;
        }
        _browserInitialized = false;
        await InitializeBrowserAsync();
    }

    private static string SafeFolderName(string url)
    {
        var name = new Uri(url).Host.Replace(".", "_");
        return string.IsNullOrEmpty(name) ? "default" : name;
    }

    public void Navigate(string url) => CloudLoginBrowser.CoreWebView2?.Navigate(url);

    private void BrowserBack_Click(object sender, RoutedEventArgs e)
    {
        if (CloudLoginBrowser.CoreWebView2?.CanGoBack == true)
            CloudLoginBrowser.CoreWebView2.GoBack();
    }

    private void BrowserForward_Click(object sender, RoutedEventArgs e)
    {
        if (CloudLoginBrowser.CoreWebView2?.CanGoForward == true)
            CloudLoginBrowser.CoreWebView2.GoForward();
    }

    private void BrowserRefresh_Click(object sender, RoutedEventArgs e) =>
        CloudLoginBrowser.CoreWebView2?.Reload();

    private void BrowserHome_Click(object sender, RoutedEventArgs e) =>
        CloudLoginBrowser.CoreWebView2?.Navigate(InitialUrl);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        CancelNavigationTimeout();
        try
        {
            if (CloudLoginBrowser.CoreWebView2 is not null)
            {
                CloudLoginBrowser.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                CloudLoginBrowser.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                if (_themeScriptId is not null)
                    CloudLoginBrowser.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(_themeScriptId);
                CloudLoginBrowser.CoreWebView2.Stop();
                CloudLoginBrowser.CoreWebView2.Navigate("about:blank");
            }
        }
        catch
        {
        }
        // 显式释放环境 COM 引用，让浏览器进程尽快退出、释放 UserDataFolder 目录锁
        // （降低下一实例初始化命中 0x8007139F 的概率；TrySuspend 在 WPF 包装的
        // CoreWebView2 上无公开 API，不可直接调用）。
        CloudLoginBrowser.Dispose();
        if (_environment is not null)
        {
            _environment = null;
        }

        _browserInitialized = false;
        _viewModel?.Dispose();
        _viewModel = null;
    }
}
