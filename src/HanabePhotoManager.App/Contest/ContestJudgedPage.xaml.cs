using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HanabePhotoManager.App.Contest;

public partial class ContestJudgedPage : System.Windows.Controls.UserControl
{
    private bool _browserInitialized;
    private static readonly ContestViewModel SharedVm = new();
    private ContestItem? _pendingDownloadContest;
    private string? _pendingDownloadFolder;
    private TaskCompletionSource<string>? _extractionTcs;

    public ContestJudgedPage()
    {
        InitializeComponent();
        JudgedContestList.ItemsSource = SharedVm.JudgedContests;
    }

    private async void ContestCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.FrameworkElement element
            || element.DataContext is not ContestItem contest)
            return;

        EmptyState.Visibility = Visibility.Collapsed;
        await InitializeBrowserAsync();
        Browser.CoreWebView2?.Navigate(contest.Url);
    }

    private async System.Threading.Tasks.Task InitializeBrowserAsync()
    {
        if (_browserInitialized) return;
        _browserInitialized = true;
        try
        {
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                userDataFolder: System.IO.Path.Combine(HanabePhotoManager.App.Services.AppDataPaths.Root, "WebView2", "ContestJudged"));
            await Browser.EnsureCoreWebView2Async(env);
            Browser.CoreWebView2.NavigationCompleted += Browser_NavigationCompleted;
        }
        catch
        {
            _browserInitialized = false;
        }
    }

    private async void DownloadAll_Click(object sender, RoutedEventArgs e)
    {
        // 1. 选目标文件夹
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择获奖作品下载目标文件夹",
            Multiselect = false
        };
        if (dlg.ShowDialog() != true) return;
        var target = dlg.FolderName;

        // 2. 选赛事
        var pick = new ContestPickerWindow(SharedVm.JudgedContests) { Owner = Window.GetWindow(this) };
        if (pick.ShowDialog() != true) return;
        var selected = pick.SelectedContest;
        if (selected is null) return;

        // 3. 抓图
        _pendingDownloadContest = selected;
        _pendingDownloadFolder = target;
        _extractionTcs = new TaskCompletionSource<string>();

        await InitializeBrowserAsync();
        Browser.CoreWebView2?.Navigate(selected.Url);

        // 4. 等 JS 回调
        var html = await _extractionTcs.Task;
        await ExtractAndDownloadAsync(selected, target, html);
    }

    private async System.Threading.Tasks.Task ExtractAndDownloadAsync(
        ContestItem contest, string target, string json)
    {
        List<string> imageUrls;
        try
        {
            imageUrls = JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }
        catch
        {
            System.Windows.MessageBox.Show("图片地址解析失败（页面可能没有图片资源）",
                "下载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (imageUrls.Count == 0)
        {
            System.Windows.MessageBox.Show("该页面没有可下载的获奖作品图片。\n请检查官网内容或更换赛事。",
                "无可下载", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dir = Path.Combine(target, Sanitize(contest.Name));
        Directory.CreateDirectory(dir);

        using var http = new System.Net.Http.HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "HanabePhotoManager/1.0");
        http.Timeout = TimeSpan.FromSeconds(30);

        int success = 0, failed = 0;
        var failedNames = new List<string>();

        for (int i = 0; i < imageUrls.Count; i++)
        {
            var url = imageUrls[i];
            try
            {
                var resp = await http.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode) { failed++; continue; }

                var bytes = await resp.Content.ReadAsByteArrayAsync();
                if (bytes.Length < 5000) { failed++; continue; }  // 跳过太小的（logo/按钮）

                var ext = Path.GetExtension(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(ext) || ext.Length > 6) ext = ".jpg";

                var idx = i + 1;
                var fn = $"{idx:000}{ext}";
                await File.WriteAllBytesAsync(Path.Combine(dir, fn), bytes);
                success++;
            }
            catch
            {
                failed++;
            }
        }

        var msg = $"赛事 {contest.Name}\n\n成功：{success} 张\n失败：{failed} 张\n保存到：\n{dir}";
        System.Windows.MessageBox.Show(msg, "下载完成",
            MessageBoxButton.OK,
            success > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void Browser_NavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_extractionTcs is null) return;

        // 等待一下让图片加载完
        _ = Task.Delay(3000).ContinueWith(async _ =>
        {
            try
            {
                var json = await Browser.CoreWebView2.ExecuteScriptAsync(GetImageExtractionScript());
                _extractionTcs.TrySetResult(json);
            }
            catch (Exception ex)
            {
                _extractionTcs.TrySetResult($"[{{\"error\":\"{ex.Message}\"}}]");
            }
        });
    }

    private static string GetImageExtractionScript() =>
        @"(function() {
            const imgs = Array.from(document.images);
            const urls = imgs
                .map(img => img.src || img.dataset.src || img.getAttribute('data-original') || '')
                .filter(u => u && (u.startsWith('http') || u.startsWith('//')))
                .map(u => u.startsWith('//') ? 'https:' + u : u)
                .filter(u => /\.(jpe?g|png|webp)(\?|$)/i.test(u))
                .filter(u => !u.includes('icon') && !u.includes('logo') && !u.includes('avatar'))
                .filter((u, i, arr) => arr.indexOf(u) === i);
            return JSON.stringify(urls);
        })()";

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(s.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrEmpty(result) ? "未命名" : result;
    }
}
