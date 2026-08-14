using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using LibVLCSharp.Shared;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace HanabePhotoManager.App;

public partial class PhotoViewerWindow : Window
{
    private readonly PhotoViewerViewModel _viewModel;
    private DispatcherTimer? _hideTimer;
    private DispatcherTimer? _positionTimer;
    private bool _isPanning;
    // 构造完成标志：ctor 里 _viewModel.Open() 会同步触发 PropertyChanged →
    // RefreshMediaDisplay → StopVideo，彼时定时器/字段尚未初始化，必须忽略构造期事件
    // （Window_Loaded 会重新 RefreshMediaDisplay 完成首帧渲染）
    private bool _initialized;
    private System.Windows.Point _panStart;
    private double _panHorizontalOffset;
    private double _panVerticalOffset;

    // LibVLC 视频引擎（仅在打开视频文件时惰性创建；窗口关闭时释放）
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private bool _videoActive;
    private bool _updatingSlider;

    // 倍速循环：0.5x → 1x → 1.25x → 1.5x → 2x（LibVLC MediaPlayer.Rate）
    private static readonly double[] SpeedValues = [0.5, 1, 1.25, 1.5, 2];
    private int _speedIndex = 1;

    // 真全屏（无边框最大化）状态；Esc 在全屏时先退出全屏再关闭
    private bool _isFullscreen;

    // 亚克力层刷新节流（RTB 截取 MediaRoot 较贵，鼠标移动期间最多每 300ms 一次）
    private DateTime _lastAcrylicRefresh = DateTime.MinValue;
    private const double AcrylicThrottleMs = 300;

    // ---------- HwndHost airspace 修复（2026-08-14 用户反馈「视频功能栏被视频盖住」） ----------
    // LibVLCSharp 的 VideoView 是 HwndHost（Win32 子窗口），天然渲染在所有 WPF 内容之上
    // （airspace 问题）：浮动工具栏/信息面板是 WPF Border，会被全窗口拉伸的视频窗口物理
    // 遮挡，既看不见也点不到。NuGet 官方 AirspaceDecorator 包（Microsoft.Wpf.Interop.Airspace）
    // 在 nuget.org 不存在（2026-08-14 实测 BlobNotFound），故采用布局外置方案：视频播放且
    // 工具栏可见时给 VideoHost 预留顶/底工具栏带 + 右侧信息面板带，让视频窗口物理上不覆盖
    // 任何 WPF 功能栏（工具栏永远可点）；工具栏隐藏（沉浸态）时视频恢复全窗口。
    private const double VideoBarReserveTop = 76;     // 顶栏 Margin16 + 高 56 + 4 安全余量
    private const double VideoBarReserveBottom = 76;  // 底栏同上
    private const double VideoBarReserveRight = 348;  // InfoPanel 340 宽 + 8 间隙

    private void UpdateVideoHostLayout()
    {
        var target = new Thickness(0);
        if (_videoActive)
        {
            // 工具栏「已调出」以 IsHitTestVisible 为准（ShowBar/HideBar 同步置位，无动画时序竞态）
            var barsVisible = TopBar.IsHitTestVisible || TopBarRight.IsHitTestVisible || BottomBar.IsHitTestVisible;
            target = new Thickness(
                0,
                barsVisible ? VideoBarReserveTop : 0,
                InfoPanel.Visibility == Visibility.Visible ? VideoBarReserveRight : 0,
                barsVisible ? VideoBarReserveBottom : 0);
        }
        if (VideoHost.Margin != target) VideoHost.Margin = target;
    }


    public PhotoViewerWindow(IEnumerable<string> paths, string selectedPath, Action<string>? photoDeleted = null)
    {
        InitializeComponent();
        _viewModel = new PhotoViewerViewModel();
        if (photoDeleted is not null) _viewModel.PhotoDeleted += photoDeleted;
        DataContext = _viewModel;

        // ⚠️ 顺序铁律：定时器必须先于 _viewModel.Open() 创建——
        // Open() 会同步触发 PropertyChanged → RefreshMediaDisplay → StopVideo()，
        // 若 _positionTimer 尚未赋值，StopVideo() 的 _positionTimer.Stop() 直接 NullReferenceException。
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hideTimer.Tick += OnHideTimerTick;

        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _positionTimer.Tick += OnPositionTimerTick;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Open(paths, selectedPath);

        // 构造完成标志（Window_Loaded 会重新走 RefreshMediaDisplay）
        _initialized = true;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 构造期间（_initialized == false）VM 事件先于字段初始化触发，一律忽略；
        // 首帧渲染由 Window_Loaded → RefreshMediaDisplay 完成
        if (!_initialized) return;
        if (e.PropertyName == nameof(PhotoViewerViewModel.IsOpen) && !_viewModel.IsOpen)
        {
            Close();
        }
        else if (e.PropertyName is nameof(PhotoViewerViewModel.CurrentPath) or nameof(PhotoViewerViewModel.IsVideo))
        {
            RefreshMediaDisplay();
        }
    }

    // ---------- 媒体切换：照片 ⇄ 视频 ----------

    private void RefreshMediaDisplay()
    {
        // 构造完成前直接 return（Window_Loaded 会重新调用；防止 StopVideo 访问未初始化字段）
        if (!_initialized) return;
        if (_viewModel.IsVideo && _viewModel.CurrentPath is { } videoPath)
            PlayVideo(videoPath);
        else
            StopVideo();
    }

    private void PlayVideo(string path)
    {
        EnsureVideoEngine();
        if (_libVlc is null) return;

        StopVideo();

        _mediaPlayer = new MediaPlayer(_libVlc);
        _mediaPlayer.Playing += OnMediaPlaying;
        _mediaPlayer.Paused += OnMediaPaused;
        _mediaPlayer.Stopped += OnMediaStopped;
        _mediaPlayer.EndReached += OnMediaEndReached;

        VideoHost.Visibility = Visibility.Visible;
        VideoHost.MediaPlayer = _mediaPlayer;
        _currentMedia = new Media(_libVlc, path, FromType.FromPath);
        _mediaPlayer.Media = _currentMedia;
        _mediaPlayer.Volume = (int)VolumeSlider.Value;
        _mediaPlayer.Play();
        ApplySpeed();

        PhotoViewport.Visibility = Visibility.Collapsed;
        VideoGroup.Visibility = Visibility.Visible;
        VideoHost.Visibility = Visibility.Visible;
        _videoActive = true;
        _updatingSlider = true;
        PositionSlider.Maximum = 1;
        PositionSlider.Value = 0;
        TimeText.Text = "00:00 / 00:00";
        _updatingSlider = false;
        UpdatePlayGlyph(isPlaying: true);
        _positionTimer?.Start();
        // 视频窗口预留工具栏带（HwndHost airspace 布局外置，见 UpdateVideoHostLayout）
        UpdateVideoHostLayout();
    }

    private void StopVideo()
    {
        _positionTimer?.Stop();
        _videoActive = false;
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Playing -= OnMediaPlaying;
            _mediaPlayer.Paused -= OnMediaPaused;
            _mediaPlayer.Stopped -= OnMediaStopped;
            _mediaPlayer.EndReached -= OnMediaEndReached;
            _mediaPlayer.Stop();
            _mediaPlayer.Dispose();
            _mediaPlayer = null;
        }
        _currentMedia?.Dispose();
        _currentMedia = null;
        VideoHost.MediaPlayer = null;
        VideoHost.Visibility = Visibility.Collapsed;
        VideoGroup.Visibility = Visibility.Collapsed;
        PhotoViewport.Visibility = Visibility.Visible;
        // 停止视频后恢复全窗口布局（margin 归零，照片/沉浸态不受影响）
        UpdateVideoHostLayout();
    }

    private void EnsureVideoEngine()
    {
        if (_libVlc is not null) return;
        try
        {
            _libVlc = new LibVLC();
        }
        catch (Exception ex)
        {
            _viewModel.ReportError($"无法初始化视频播放引擎（libvlc）：{ex.Message}");
        }
    }

    // LibVLC 事件回调运行在 VLC 内部线程（非 UI 线程），触碰 WPF 元素必须 marshal 到 UI 线程，
    // 否则 DispatcherObject 跨线程访问抛 InvalidOperationException（实测 OnMediaPlaying → UpdatePlayGlyph 崩溃）
    private void OnUiThread(Action action)
    {
        if (Dispatcher.CheckAccess()) action();
        else Dispatcher.BeginInvoke(action);
    }

    private void OnMediaPlaying(object? sender, EventArgs e) => OnUiThread(() =>
    {
        UpdatePlayGlyph(isPlaying: true);
        // VLC 在切媒体/重新播放时可能重置 Rate，Playing 事件里按当前选择重放
        ApplySpeed();
    });

    private void OnMediaPaused(object? sender, EventArgs e) => OnUiThread(() => UpdatePlayGlyph(isPlaying: false));

    private void OnMediaStopped(object? sender, EventArgs e) => OnUiThread(() => UpdatePlayGlyph(isPlaying: false));

    private void OnMediaEndReached(object? sender, EventArgs e) => OnUiThread(() =>
    {
        UpdatePlayGlyph(isPlaying: false);
        _positionTimer?.Stop();
    });

    private void UpdatePlayGlyph(bool isPlaying)
    {
        PlayGlyph.Visibility = isPlaying ? Visibility.Collapsed : Visibility.Visible;
        PauseGlyph.Visibility = isPlaying ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- 视频控制 ----------

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayer is not { } player || !_videoActive) return;
        if (player.IsPlaying) player.Pause();
        else player.Play();
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (_mediaPlayer is not { } player || !_videoActive) return;
        _updatingSlider = true;
        try
        {
            var length = Math.Max(1, player.Length);
            PositionSlider.Maximum = length;
            PositionSlider.Value = Math.Clamp(player.Time, 0, length);
            TimeText.Text = $"{FormatTime(player.Time)} / {FormatTime(length)}";
        }
        finally
        {
            _updatingSlider = false;
        }
    }

    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingSlider || _mediaPlayer is not { } player || !_videoActive) return;
        player.Time = (long)Math.Clamp(e.NewValue, 0, Math.Max(0, player.Length));
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_mediaPlayer is { } player) player.Volume = (int)e.NewValue;
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayer is not { } player) return;
        player.Mute = !player.Mute;
        MutedGlyph.Visibility = player.Mute ? Visibility.Visible : Visibility.Collapsed;
        VolumeGlyph.Visibility = player.Mute ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------- 快进 / 后退（±10 秒，LibVLC Time 毫秒级 seek） ----------

    private void SeekBack_Click(object sender, RoutedEventArgs e) => SeekRelative(-10_000);

    private void SeekForward_Click(object sender, RoutedEventArgs e) => SeekRelative(10_000);

    private void SeekRelative(long deltaMilliseconds)
    {
        if (_mediaPlayer is not { } player || !_videoActive) return;
        var length = Math.Max(0, player.Length);
        player.Time = Math.Clamp(player.Time + deltaMilliseconds, 0, length);
    }

    // ---------- 倍速（0.5x / 1x / 1.25x / 1.5x / 2x 循环） ----------

    private void Speed_Click(object sender, RoutedEventArgs e)
    {
        SpeedCycle(1);
    }

    private void SpeedCycle(int delta)
    {
        var n = SpeedValues.Length;
        _speedIndex = ((_speedIndex + delta) % n + n) % n;
        ApplySpeed();
        ShowOverlays();
        _hideTimer?.Stop();
        _hideTimer?.Start();
    }

    private void ApplySpeed()
    {
        SpeedText.Text = FormatRate(SpeedValues[_speedIndex]);
        if (_mediaPlayer is not { } player || !_videoActive) return;
        try { player.SetRate((float)SpeedValues[_speedIndex]); }
        catch { /* 个别媒体/容器不支持倍速时保持 1x 不崩溃 */ }
    }

    private static string FormatRate(double rate) =>
        Math.Abs(rate - Math.Round(rate)) < 0.001 ? $"{rate:0}x" : $"{rate:0.##}x";

    private static string FormatTime(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"mm\:ss");
    }

    // ---------- 沉浸式工具栏（默认零标识，鼠标移动调出，3 秒自动隐藏） ----------

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        ShowOverlays();
        _hideTimer?.Stop();
        _hideTimer?.Start();
    }

    private void OnHideTimerTick(object? sender, EventArgs e)
    {
        if (InfoPanel.Visibility == Visibility.Visible)
        {
            _hideTimer?.Start();
            return;
        }
        if (IsOverlayHot(TopBar) || IsOverlayHot(TopBarRight) || IsOverlayHot(BottomBar))
        {
            _hideTimer?.Start();
            return;
        }
        HideOverlays();
    }

    private static bool IsOverlayHot(FrameworkElement bar) =>
        bar.Visibility == Visibility.Visible && bar.Opacity >= 0.5 && bar.IsHitTestVisible && bar.IsMouseOver;

    private void ShowOverlays()
    {
        ShowBar(TopBar);
        ShowBar(TopBarRight);
        ShowBar(BottomBar);
        RefreshAcrylic();
        AnimateOpacity(AcrylicLayer, 1);
        // 工具栏调出 → 视频让出工具栏带，保证功能栏可见可点（airspace 布局外置）
        UpdateVideoHostLayout();
    }

    private void HideOverlays()
    {
        HideBar(TopBar);
        HideBar(TopBarRight);
        HideBar(BottomBar);
        AnimateOpacity(AcrylicLayer, 0);
        // 工具栏隐藏 → 沉浸态，视频恢复全窗口
        UpdateVideoHostLayout();
    }

    // ---------- 亚克力玻璃层：截取媒体区 + 模糊，按工具栏胶囊几何裁剪 ----------

    /// <summary>
    /// 把 MediaRoot 实时渲染为位图作为 AcrylicLayer 背景（BlurEffect 提供模糊），
    /// OpacityMask 按各可见工具栏胶囊的窗口坐标裁剪——只有胶囊内露出模糊玻璃，
    /// 照片主体保持清晰。视频模式下 HwndHost 截取为黑区，呈深色玻璃（符合黑底语义）。
    /// </summary>
    private void RefreshAcrylic(bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && (now - _lastAcrylicRefresh).TotalMilliseconds < AcrylicThrottleMs) return;
        _lastAcrylicRefresh = now;

        var width = (int)Math.Ceiling(MediaRoot.ActualWidth);
        var height = (int)Math.Ceiling(MediaRoot.ActualHeight);
        if (width <= 1 || height <= 1) return;
        try
        {
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(MediaRoot);
            bitmap.Freeze();
            var image = new ImageBrush(bitmap)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top
            };
            image.Freeze();
            AcrylicLayer.Background = image;

            // 胶囊几何（窗口绝对坐标，与 ImageBrush 1:1 对应）
            var geometry = new GeometryGroup();
            foreach (var bar in new[] { TopBar, TopBarRight, BottomBar })
            {
                if (bar.Visibility != Visibility.Visible || bar.ActualWidth <= 0 || bar.ActualHeight <= 0) continue;
                var topLeft = bar.TranslatePoint(new System.Windows.Point(0, 0), this);
                // 与 Viewer.Toolbar 的 CornerRadius（Radius.Container=28）保持一致；小条时不超过半高
                var pillRadius = 28d;
                if (TryFindResource("Radius.Container") is CornerRadius tokenRadius && tokenRadius.TopLeft > 0)
                {
                    pillRadius = tokenRadius.TopLeft;
                }
                var radius = Math.Min(pillRadius, bar.ActualHeight / 2);
                geometry.Children.Add(new RectangleGeometry(
                    new Rect(topLeft.X, topLeft.Y, bar.ActualWidth, bar.ActualHeight), radius, radius));
            }

            var mask = new DrawingBrush(new GeometryDrawing(System.Windows.Media.Brushes.White, null, geometry))
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                Viewbox = new Rect(0, 0, AcrylicLayer.ActualWidth, AcrylicLayer.ActualHeight),
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, AcrylicLayer.ActualWidth, AcrylicLayer.ActualHeight),
                ViewportUnits = BrushMappingMode.Absolute
            };
            mask.Freeze();
            AcrylicLayer.OpacityMask = mask;
        }
        catch
        {
            // 截取失败时保持上次玻璃层，不阻断工具栏显示
        }
    }

    // ---------- 全屏（F11 / 按钮 / 双击空白区，Esc 退出） ----------

    private void ToggleFullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void ToggleFullscreen()
    {
        _isFullscreen = !_isFullscreen;
        WindowState = _isFullscreen ? WindowState.Maximized : WindowState.Normal;
        FullscreenGlyph.Visibility = _isFullscreen ? Visibility.Collapsed : Visibility.Visible;
        RestoreGlyph.Visibility = _isFullscreen ? Visibility.Visible : Visibility.Collapsed;
        ShowOverlays();
        _hideTimer?.Stop();
        _hideTimer?.Start();
    }

    private static void ShowBar(FrameworkElement bar)
    {
        bar.Visibility = Visibility.Visible;
        bar.IsHitTestVisible = true;
        AnimateOpacity(bar, 1);
    }

    private static void HideBar(FrameworkElement bar)
    {
        bar.IsHitTestVisible = false;
        AnimateOpacity(bar, 0);
    }

    private static void AnimateOpacity(FrameworkElement element, double to)
    {
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    // ---------- 详细信息面板 ----------

    private void ToggleInfo_Click(object sender, RoutedEventArgs e)
    {
        if (InfoPanel.Visibility == Visibility.Visible) HideInfoPanel();
        else ShowInfoPanel();
    }

    private void ShowInfoPanel()
    {
        InfoPanel.Visibility = Visibility.Visible;
        InfoPanel.RenderTransform = new TranslateTransform(24, 0);
        var slide = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        InfoPanel.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slide);
        AnimateOpacity(InfoPanel, 1);
        ShowOverlays();
        _hideTimer?.Stop();
    }

    private void HideInfo_Click(object sender, RoutedEventArgs e) => HideInfoPanel();

    private void HideInfoPanel()
    {
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        fade.Completed += (_, _) =>
        {
            InfoPanel.Visibility = Visibility.Collapsed;
            // 信息面板收起后再释放右侧视频带（避免视频在面板淡出时从底下扩张）
            UpdateVideoHostLayout();
        };
        InfoPanel.BeginAnimation(UIElement.OpacityProperty, fade);
        ShowOverlays();
        _hideTimer?.Start();
    }

    // ---------- 工具栏动作 ----------

    private void Previous_Click(object sender, RoutedEventArgs e) => _viewModel.Previous();

    private void Next_Click(object sender, RoutedEventArgs e) => _viewModel.Next();

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => _viewModel.AdjustZoom(1);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => _viewModel.AdjustZoom(-1);

    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetZoom();
        PhotoViewport.ScrollToHome();
    }

    private void OpenSourceFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentPath is not { } path || !File.Exists(path)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // 资源管理器打开失败时静默（不影响看图）
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Delete_Click(object sender, RoutedEventArgs e) => ConfirmDeleteCurrent();

    private void ConfirmDeleteCurrent()
    {
        var name = _viewModel.Metadata.Name;
        if (!DeleteConfirmationWindow.Confirm(
                this,
                $"确定把 {name} 移入回收站吗？",
                selectedCount: 1,
                actualFileCount: 1))
            return;

        _viewModel.DeleteCurrent();
    }

    // ---------- 键盘 ----------

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Up)
        {
            // 视频播放中 Shift+← = 后退 10 秒；否则上一张
            if (_videoActive && (Keyboard.Modifiers & ModifierKeys.Shift) != 0) SeekRelative(-10_000);
            else _viewModel.Previous();
            e.Handled = true;
        }
        else if (e.Key is Key.Right or Key.Down)
        {
            if (_videoActive && (Keyboard.Modifiers & ModifierKeys.Shift) != 0) SeekRelative(10_000);
            else _viewModel.Next();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete) { ConfirmDeleteCurrent(); e.Handled = true; }
        else if (e.Key == Key.Escape)
        {
            // 全屏时 Esc 先退出全屏，再按一次才关闭
            if (_isFullscreen) ToggleFullscreen();
            else Close();
            e.Handled = true;
        }
        else if (e.Key == Key.F11) { ToggleFullscreen(); e.Handled = true; }
        else if (e.Key == Key.Space && _videoActive)
        {
            PlayPause_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.OemOpenBrackets && _videoActive) { SpeedCycle(-1); e.Handled = true; }
        else if (e.Key == Key.OemCloseBrackets && _videoActive) { SpeedCycle(1); e.Handled = true; }
        else if (e.Key is >= Key.D1 and <= Key.D5) { _viewModel.SetRating((int)e.Key - (int)Key.D0); e.Handled = true; }
        else if (e.Key is >= Key.NumPad1 and <= Key.NumPad5) { _viewModel.SetRating((int)e.Key - (int)Key.NumPad0); e.Handled = true; }
    }

    // ---------- 照片缩放 / 拖拽平移 ----------

    private void PhotoViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var pointer = e.GetPosition(PhotoViewport);
        var oldWidth = PhotoViewport.ExtentWidth;
        var oldHeight = PhotoViewport.ExtentHeight;
        var oldHorizontal = PhotoViewport.HorizontalOffset;
        var oldVertical = PhotoViewport.VerticalOffset;
        _viewModel.AdjustZoom(Math.Sign(e.Delta));
        Dispatcher.BeginInvoke(new Action(() =>
        {
            PhotoViewport.UpdateLayout();
            PhotoViewport.ScrollToHorizontalOffset(PhotoViewportMath.AnchoredOffset(
                oldWidth, PhotoViewport.ExtentWidth, oldHorizontal, pointer.X, PhotoViewport.ScrollableWidth));
            PhotoViewport.ScrollToVerticalOffset(PhotoViewportMath.AnchoredOffset(
                oldHeight, PhotoViewport.ExtentHeight, oldVertical, pointer.Y, PhotoViewport.ScrollableHeight));
            RefreshAcrylic(force: true);
        }), DispatcherPriority.Loaded);
        e.Handled = true;
    }

    private void PhotoViewport_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _viewModel.ResetZoom();
            PhotoViewport.ScrollToHome();
            e.Handled = true;
            return;
        }
        _isPanning = true;
        _panStart = e.GetPosition(this);
        _panHorizontalOffset = PhotoViewport.HorizontalOffset;
        _panVerticalOffset = PhotoViewport.VerticalOffset;
        PhotoViewport.Cursor = System.Windows.Input.Cursors.SizeAll;
        PhotoViewport.CaptureMouse();
        e.Handled = true;
    }

    private void PhotoViewport_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(this);
        PhotoViewport.ScrollToHorizontalOffset(PhotoViewportMath.DragOffset(
            _panHorizontalOffset, current.X - _panStart.X, PhotoViewport.ScrollableWidth));
        PhotoViewport.ScrollToVerticalOffset(PhotoViewportMath.DragOffset(
            _panVerticalOffset, current.Y - _panStart.Y, PhotoViewport.ScrollableHeight));
        e.Handled = true;
    }

    private void PhotoViewport_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndPan();
    private void PhotoViewport_LostMouseCapture(object sender, MouseEventArgs e) => EndPan();

    private void EndPan()
    {
        if (!_isPanning) return;
        _isPanning = false;
        PhotoViewport.ReleaseMouseCapture();
        PhotoViewport.Cursor = System.Windows.Input.Cursors.Hand;
        RefreshAcrylic(force: true);
    }

    // ---------- 窗口拖拽（无边框窗口移动） ----------

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Button) return;
        var position = e.GetPosition(this);
        if (IsPointOver(InfoPanel, position) || IsPointOver(TopBar, position) ||
            IsPointOver(TopBarRight, position) || IsPointOver(BottomBar, position))
            return;
        if (e.ClickCount == 2)
            // 双击空白区 = 全屏切换（照片区双击 = 适应窗口，已在 PhotoViewport 预览事件短路）
            ToggleFullscreen();
        else if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private bool IsPointOver(FrameworkElement element, System.Windows.Point point)
    {
        if (element.Visibility != Visibility.Visible || !element.IsHitTestVisible) return false;
        var topLeft = element.TranslatePoint(new System.Windows.Point(0, 0), this);
        return point.X >= topLeft.X - 4 &&
               point.Y >= topLeft.Y - 4 &&
               point.X <= topLeft.X + element.ActualWidth + 4 &&
               point.Y <= topLeft.Y + element.ActualHeight + 4;
    }

    // ---------- 生命周期 ----------

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshMediaDisplay();
        // 初始为沉浸态：零标识，仅当鼠标移动才调出工具栏
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initialized) return;
        // 窗口尺寸变化（含全屏切换）后重截亚克力背景，避免玻璃层错位
        Dispatcher.BeginInvoke(new Action(() => RefreshAcrylic(force: true)), DispatcherPriority.Loaded);
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _positionTimer?.Stop();
        _hideTimer?.Stop();
        StopVideo();
        _libVlc?.Dispose();
        _libVlc = null;
    }

    // ---------- 截图模式（--viewer + --screenshot） ----------

    /// <summary>
    /// 渲染当前查看器窗口到 PNG。视频文件先取 VLC 快照替换 VideoView
    /// （HwndHost 内容不进 WPF 视觉树，RenderTargetBitmap 只能截到黑区），
    /// 这样截图里能看到真实视频帧 + 悬浮工具栏/控制栏。
    /// <paramref name="showOverlays"/> 为 false 时保持零标识沉浸态（纯照片/纯视频）。
    /// </summary>
    public async Task CaptureScreenshotAsync(string outPath, bool showOverlays = true)
    {
        if (InfoPanel.Visibility == Visibility.Visible) HideInfoPanel();
        // 先取 VLC 帧快照并收起 VideoHost，再调出工具栏——亚克力层此时截到的是
        // 真实视频帧（HwndHost 内容不进 RenderTargetBitmap，否则玻璃层和截图都是黑区）
        if (_viewModel.IsVideo && _mediaPlayer is { } player)
        {
            var temp = Path.Combine(Path.GetTempPath(), $"hanabe-vlc-frame-{Guid.NewGuid():N}.png");
            try
            {
                // 无条件尝试快照：即使已暂停/片尾（2s 夹具视频在 3s 截图延迟后 EndReached）
                // 也尽量取当前帧；失败则保留黑区不阻断截图
                if (player.TakeSnapshot(0, temp, 0, 0) && File.Exists(temp))
                {
                    var frame = new BitmapImage();
                    frame.BeginInit();
                    frame.CacheOption = BitmapCacheOption.OnLoad;
                    frame.UriSource = new Uri(temp);
                    frame.EndInit();
                    frame.Freeze();
                    PhotoImage.Source = frame;
                    PhotoViewport.Visibility = Visibility.Visible;
                    VideoHost.Visibility = Visibility.Collapsed;
                    VideoHost.MediaPlayer = null;
                    player.Stop();
                    _positionTimer?.Stop();
                }
            }
            catch
            {
                // 快照失败则保留黑区，不阻断截图
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { /* 忽略临时文件清理失败 */ }
            }
        }

        if (showOverlays) ShowOverlays();
        else HideOverlays();

        // 等工具栏 150ms 淡入动画走完再渲染——Dispatcher 空闲时（ApplicationIdle）动画时钟
        // 尚未推进，直接渲染会截到 Opacity≈0 的不可见工具栏（2026-08-14 药丸截图实测）；
        // Task.Delay 让出 UI 线程给渲染 tick，300ms > 150ms 动画时长。
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        UpdateLayout();
        RenderWindowToPng(outPath);
    }

    private void RenderWindowToPng(string path)
    {
        try
        {
            var bitmap = new RenderTargetBitmap(
                Math.Max(1, (int)Math.Ceiling(ActualWidth)),
                Math.Max(1, (int)Math.Ceiling(ActualHeight)),
                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
        finally
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}

/// <summary>
/// M3 滑块进度条宽度：value / maximum × 轨道宽度。
/// </summary>
public sealed class ViewerSliderWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 3 && values[0] is double width && values[1] is double value && values[2] is double maximum && width > 0)
        {
            var max = Math.Max(1, maximum);
            var ratio = Math.Clamp(value / max, 0, 1);
            return Math.Max(0, width * ratio);
        }
        return 0d;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
