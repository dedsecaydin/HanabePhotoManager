using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.App.Services;
using Microsoft.Web.WebView2.Core;

namespace HanabePhotoManager.App.Map;

public partial class MapPage : System.Windows.Controls.UserControl, IDisposable
{
    private MapPhotosViewModel? _viewModel;
    private bool _initialized;
    private readonly MapThumbnailCache _thumbnailCache = new();
    private int _markerGeneration;

    public MapPage()
    {
        InitializeComponent();
        Loaded += MapPage_Loaded;
        DataContextChanged += MapPage_DataContextChanged;
    }

    private async void MapPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) { SendMarkers(); return; }

        ShowLoadingState("正在加载地图", "正在初始化地图引擎，请稍候。");
        try
        {
            await InitializeWebViewAsync();
        }
        catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x800700AA)
        {
            // ERROR_BUSY — parent window not ready for WebView2 yet. Defer init to next dispatcher cycle.
            _ = Dispatcher.BeginInvoke(async () =>
            {
                try { await Task.Yield(); await InitializeWebViewAsync(); }
                catch (Exception retryEx) { ShowErrorState("地图加载失败", retryEx.Message); }
            });
        }
        catch (Exception ex)
        {
            ShowErrorState("地图加载失败", ex.Message);
        }
    }

    private async Task InitializeWebViewAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder:
            Path.Combine(AppDataPaths.Root, "WebView2", "Map"));
        await MapWebView.EnsureCoreWebView2Async(environment);
        var assetFolder = Path.Combine(AppContext.BaseDirectory, "Map", "assets");
        MapWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "hanabe-map.local", assetFolder, CoreWebView2HostResourceAccessKind.DenyCors);
        System.IO.Directory.CreateDirectory(_thumbnailCache.Directory);
        MapWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "hanabe-thumbs.local", _thumbnailCache.Directory, CoreWebView2HostResourceAccessKind.DenyCors);
        MapWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        MapWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        MapWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        MapWebView.CoreWebView2.NavigationCompleted += (_, args) =>
        {
            if (args.IsSuccess) ShowContentState();
            else ShowErrorState("地图加载失败", $"无法加载地图页面（{args.WebErrorStatus}）。");
            SendMarkers();
        };
        MapWebView.Source = new Uri("https://hanabe-map.local/index.html");
        _initialized = true;
    }

    private void ShowLoadingState(string title, string description)
    {
        MapStatusPanel.Visibility = Visibility.Visible;
        MapStatusTitle.Text = title;
        MapStatusDescription.Text = description;
        MapRetryButton.Visibility = Visibility.Collapsed;
    }

    private void ShowContentState()
    {
        MapStatusPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowErrorState(string title, string description)
    {
        MapStatusPanel.Visibility = Visibility.Visible;
        MapStatusTitle.Text = title;
        MapStatusDescription.Text = description;
        MapRetryButton.Visibility = Visibility.Visible;
    }

    private async void MapRetry_Click(object sender, RoutedEventArgs e)
    {
        if (MapWebView.CoreWebView2 is not null)
        {
            // WebView2 runtime already up; only the page failed to load. Re-navigate.
            ShowLoadingState("正在加载地图", "正在重新加载地图页面，请稍候。");
            _initialized = true;
            try
            {
                MapWebView.Source = new Uri("https://hanabe-map.local/index.html");
                return;
            }
            catch (Exception)
            {
                // Fall through to a full re-initialization below.
            }
        }

        _initialized = false;
        ShowLoadingState("正在加载地图", "正在重新初始化地图引擎，请稍候。");
        try
        {
            await InitializeWebViewAsync();
        }
        catch (Exception ex)
        {
            ShowErrorState("地图加载失败", ex.Message);
        }
    }

    private void MapPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null) _viewModel.MarkersChanged -= ViewModel_MarkersChanged;
        _viewModel = e.NewValue as MapPhotosViewModel;
        if (_viewModel is not null) _viewModel.MarkersChanged += ViewModel_MarkersChanged;
        SendMarkers();
    }

    private void ViewModel_MarkersChanged(object? sender, EventArgs e) => SendMarkers();

    private async void SendMarkers()
    {
        if (!_initialized || MapWebView.CoreWebView2 is null || _viewModel is null) return;
        var generation = ++_markerGeneration;
        var markerTasks = _viewModel.Markers.Select(async marker => new
        {
            marker.Id, marker.Latitude, marker.Longitude, marker.Count,
            PreviewUrls = (await Task.WhenAll(_viewModel.GetMarkerPhotoPaths(marker.Id, 3)
                .Select(path => _thumbnailCache.GetUrlAsync(path)))).Where(url => url is not null).ToArray()
        });
        var markers = await Task.WhenAll(markerTasks);
        if (generation != _markerGeneration || MapWebView.CoreWebView2 is null) return;
        var payload = JsonSerializer.Serialize(new { type = "setMarkers", markers });
        MapWebView.CoreWebView2.PostWebMessageAsJson(payload);
    }

    private async Task ShowClusterAsync(string markerId)
    {
        if (_viewModel is null || MapWebView.CoreWebView2 is null) return;
        var paths = _viewModel.GetMarkerPhotoPaths(markerId, 40);
        var urls = (await Task.WhenAll(paths.Select(path => _thumbnailCache.GetUrlAsync(path))))
            .Where(url => url is not null).ToArray();
        MapWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(
            new { type = "showCluster", markerId, urls, total = _viewModel.GetMarkerPhotoPaths(markerId, 100).Count }));
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_viewModel is null) return;
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (root.TryGetProperty("type", out var type) && type.GetString() == "mapClick"
                && root.TryGetProperty("latitude", out var latitude)
                && root.TryGetProperty("longitude", out var longitude))
                _viewModel.SetPendingMapPoint(latitude.GetDouble(), longitude.GetDouble());
            else if (type.GetString() == "markerClick"
                && root.TryGetProperty("markerId", out var markerId)
                && markerId.GetString() is { } id)
            {
                _viewModel.SelectMarker(id);
                _ = ShowClusterAsync(id);
            }
            else if (type.GetString() == "photoClick"
                && root.TryGetProperty("markerId", out var photoMarker)
                && root.TryGetProperty("index", out var photoIndex)
                && photoMarker.GetString() is { } selectedMarker)
            {
                var paths = _viewModel.GetMarkerPhotoPaths(selectedMarker, 40);
                var index = photoIndex.GetInt32();
                if (index >= 0 && index < paths.Count
                    && Window.GetWindow(this)?.DataContext is MainWindowViewModel main)
                    new PhotoViewerWindow(paths, paths[index], main.RemoveDeletedViewerPhoto)
                    { Owner = Window.GetWindow(this) }.Show();
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { }
    }

    private async void ChooseMapPhotos_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "照片|*.jpg;*.jpeg;*.png;*.webp;*.tif;*.tiff|所有文件|*.*"
        };
        if (dialog.ShowDialog() == true && _viewModel is not null)
            await _viewModel.AddSourcesAsync(dialog.FileNames, recursive: false);
    }

    private async void ChooseMapFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择照片文件夹（自动扫描全部子文件夹）",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && _viewModel is not null)
            await _viewModel.AddSourcesAsync([dialog.SelectedPath], recursive: true);
    }

    private void SelectAllMapPhotos_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.SelectAllUnlocated();
        ManualPhotosList.SelectAll();
    }

    private void InvertMapPhotos_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.InvertUnlocatedSelection();
        ManualPhotosList.SelectedItems.Clear();
        if (_viewModel is null) return;
        foreach (var item in _viewModel.UnlocatedPhotos.Where(item => item.IsSelected))
            ManualPhotosList.SelectedItems.Add(item);
    }

    private void ClearMapPhotos_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ClearSelection();
        ManualPhotosList.UnselectAll();
    }

    private async void RemoveSelectedMapPhotos_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null) await _viewModel.RemoveSelectedSourcesAsync();
    }

    private async void ClearImportedMapPhotos_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null) await _viewModel.ClearImportedSourcesAsync();
    }

    private void ManualMapPhotos_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void ManualMapPhotos_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (_viewModel is not null
            && e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths)
            await _viewModel.AddSourcesAsync(paths, recursive: true);
        e.Handled = true;
    }

    private void ManualPhotosList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null) return;
        foreach (var item in _viewModel.UnlocatedPhotos) item.IsSelected = ManualPhotosList.SelectedItems.Contains(item);
    }

    private void MapMode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.RadioButton radioButton ||
            LocationBrowseContent is null ||
            ManualMarkContent is null)
        {
            return;
        }

        var showManualMarking = string.Equals(radioButton.Content as string, "手动标记", StringComparison.Ordinal);
        LocationBrowseContent.Visibility = showManualMarking ? Visibility.Collapsed : Visibility.Visible;
        ManualMarkContent.Visibility = showManualMarking ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LocationPhotosList_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (LocationPhotosList.SelectedItem is not MapPhotoItemViewModel selected || _viewModel is null) return;
        if (Window.GetWindow(this)?.DataContext is MainWindowViewModel main)
            new PhotoViewerWindow(_viewModel.SelectedLocationPhotos.Select(item => item.Path), selected.Path, main.RemoveDeletedViewerPhoto)
            { Owner = Window.GetWindow(this) }.Show();
    }

    public void Dispose()
    {
        if (_viewModel is not null) _viewModel.MarkersChanged -= ViewModel_MarkersChanged;
        if (MapWebView.CoreWebView2 is not null)
            MapWebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
        MapWebView.Dispose();
    }
}
