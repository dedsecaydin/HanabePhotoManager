using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

    private bool _browserInitialized;
    private bool _hasBeenVisible;
    private bool _disposed;
    private CancellationTokenSource? _navigationTimeout;

    public CloudPage()
    {
        InitializeComponent();
    }

    private static void OnIsActiveChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CloudPage page)
        {
            _ = page.ApplyActiveStateAsync((bool)e.NewValue);
        }
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
        if (_browserInitialized) return;
        _browserInitialized = true;
        ShowLoadingState("正在加载云服务", "正在准备安全的内嵌浏览器，请稍候。");

        try
        {
            var userData = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HanabePhotoManager", "WebView2",
                SafeFolderName(InitialUrl));

            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                userDataFolder: userData);
            await CloudLoginBrowser.EnsureCoreWebView2Async(env);

            CloudLoginBrowser.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
            CloudLoginBrowser.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
            CloudLoginBrowser.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            CloudLoginBrowser.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

            // 伪装 Chrome UA + 独立 user data 目录（避免 cookie 串扰）
            CloudLoginBrowser.CoreWebView2.Settings.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

            CloudLoginBrowser.CoreWebView2?.Navigate(InitialUrl);
        }
        catch (Exception ex)
        {
            _browserInitialized = false;
            ShowErrorState("云服务暂时无法打开", $"内嵌浏览器初始化失败：{ex.Message}");
            System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex.Message}");
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

    public void Navigate(string url)
    {
        CloudLoginBrowser.CoreWebView2?.Navigate(url);
    }

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

    private void BrowserRefresh_Click(object sender, RoutedEventArgs e)
    {
        CloudLoginBrowser.CoreWebView2?.Reload();
    }

    private void BrowserHome_Click(object sender, RoutedEventArgs e)
    {
        CloudLoginBrowser.CoreWebView2?.Navigate(InitialUrl);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelNavigationTimeout();
        try
        {
            if (CloudLoginBrowser.CoreWebView2 is not null)
            {
                CloudLoginBrowser.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                CloudLoginBrowser.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
            }
            CloudLoginBrowser.CoreWebView2?.Stop();
            CloudLoginBrowser.CoreWebView2?.Navigate("about:blank");
        }
        catch
        {
        }

        CloudLoginBrowser.Dispose();
        _browserInitialized = false;
    }
}
