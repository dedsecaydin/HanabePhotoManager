using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HanabePhotoManager.App.Services;
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
            }
            else
            {
                CloudLoginBrowser.CoreWebView2?.Resume();
                await ApplyThemeAsync(Interlocked.Increment(ref _themeVersion));
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

    private async Task InitializeBrowserAsync()
    {
        if (_browserInitialized || _disposed) return;
        _browserInitialized = true;
        ShowLoadingState("正在加载云服务", "正在准备安全的内嵌浏览器，请稍候。");

        try
        {
            var userData = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HanabePhotoManager", "WebView2", SafeFolderName(InitialUrl));
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await CloudLoginBrowser.EnsureCoreWebView2Async(environment);

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
        catch (Exception ex)
        {
            _browserInitialized = false;
            ShowErrorState("云服务暂时无法打开", $"内嵌浏览器初始化失败：{ex.Message}");
            System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex.Message}");
        }
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
        CloudLoginBrowser.Dispose();
        _browserInitialized = false;
    }
}
