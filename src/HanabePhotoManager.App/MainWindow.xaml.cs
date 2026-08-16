using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HanabePhotoManager.App.Browsing.Grid;
using HanabePhotoManager.App.Browsing.Treemap;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App;

public partial class MainWindow : Window
{
    private static readonly ImageSource DefaultAppIcon = BitmapFrame.Create(
        new Uri("pack://application:,,,/Assets/HanabeApp.ico", UriKind.Absolute));

    private readonly MainWindowViewModel _viewModel = new();
    private PreviewFileViewModel? _selectionAnchor;
    private System.Windows.Point? _rubberBandStart;
    private Dictionary<PreviewFileViewModel, bool> _rubberBandBaseline = [];
    private readonly EdgeAutoScrollPolicy _edgeAutoScrollPolicy = new();
    private readonly DispatcherTimer _rubberBandAutoScrollTimer;
    private readonly DispatcherTimer _windowStateSaveTimer;

    // Single-click vs double-click disambiguation: a plain single click only
    // previews in the Inspector, but it must not fire before a double click is
    // ruled out (the user explicitly rejected "double click = click + open").
    private readonly DispatcherTimer _singleClickPreviewTimer;
    private readonly DispatcherTimer _importTipTimer;
    private bool _importTipShowingSecond;
    private PreviewFileViewModel? _pendingSingleClickFile;
    private bool _pendingSingleClickControl;
    private bool _pendingSingleClickShift;
    private System.Windows.Point? _navigationDragStart;
    private NavigationItemViewModel? _navigationDragSource;
    private bool _isGridPanning;
    private System.Windows.Point _gridPanStartPoint;
    private double _gridPanStartVerticalOffset;
    private double _gridPanStartHorizontalOffset;

    private bool _isSpaceHeld;
    private bool _isSpacePanning;
    private System.Windows.Point _spacePanStartPoint;
    private double _spacePanStartVerticalOffset;
    private double _spacePanStartHorizontalOffset;

    private const double TreemapZoomMin = 0.02;
    private const double TreemapZoomMax = 8.0;
    private const double TreemapZoomNotchFactor = 1.12;
    private DispatcherTimer? _treemapZoomAnimation;
    private int _galleryZoomGeneration;

    // Title-bar theming: DWMWA_USE_IMMERSIVE_DARK_MODE makes the system caption
    // follow the app's Light/Dark theme (20 = Win10 2004+/Win11, 19 = 1809-1909).
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20h1 = 19;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(System.IntPtr hwnd, int attribute, ref int value, int valueSize);

    // ---------- DWM 亚克力/Blur 背景材质（DWM 优先 + 降级） ----------
    // 优先级：① Win11 22000+ 的 DWMWA_SYSTEMBACKDROP_TYPE（3=亚克力 / 2=Blur）
    //        ② Win10 1803+ 的 SetWindowCompositionAttribute ACCENT_ENABLE_ACRYLICBLURBEHIND
    //        ③ 均失败 → 保持现有应用内半透明玻璃方案（PanelOpacity 由 GlassIntensity 驱动）。
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmSystemBackdropNone = 0;
    private const int DwmSystemBackdropBlur = 2;    // DWMSBT_TRANSIENTWINDOW
    private const int DwmSystemBackdropAcrylic = 3; // DWMSBT_TABBEDWINDOW（亚克力）

    private const int WcaAccentPolicy = 19;
    private const int AccentDisable = 0;
    private const int AccentEnableAcrylicBlurBehind = 4;

    private bool _dwmMaterialActive;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor; // ABGR
        public int AnimationId;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public System.IntPtr Data;
        public int SizeOfData;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", PreserveSig = true)]
    private static extern int SetWindowCompositionAttribute(System.IntPtr hwnd, ref WindowCompositionAttributeData data);

    /// <summary>应用主窗口背景材质：DWM 亚克力/Blur 优先，失败自动降级为半透明方案。</summary>
    private void ApplyWindowMaterial()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == System.IntPtr.Zero) return;

            var enable = _viewModel.IsAcrylicEnabled;
            // 截图模式：RenderTargetBitmap 抓不到 DWM 背景，保持不透明底避免截图失真。
            if (App.ScreenshotPath is not null) enable = false;

            _dwmMaterialActive = false;
            if (enable)
            {
                _dwmMaterialActive = TryApplySystemBackdrop(hwnd) || TryApplyAccentAcrylic(hwnd);
            }
            else
            {
                ResetSystemBackdrop(hwnd);
                DisableAccentAcrylic(hwnd);
            }

            // 亚克力生效时窗口底层透出 DWM 背景；否则恢复不透明画布色（半透明降级方案）。
            if (_dwmMaterialActive)
            {
                Background = System.Windows.Media.Brushes.Transparent;
            }
            else
            {
                SetResourceReference(BackgroundProperty, "Brush.Background.Canvas");
            }
        }
        catch
        {
            // Non-fatal：任何 DWM 调用失败都回退现有半透明玻璃方案。
            _dwmMaterialActive = false;
            try { SetResourceReference(BackgroundProperty, "Brush.Background.Canvas"); } catch { }
        }
    }

    private bool TryApplySystemBackdrop(System.IntPtr hwnd)
    {
        // Win11 22000+：先试系统亚克力（3），失败再试 Blur（2）。
        var backdrop = DwmSystemBackdropAcrylic;
        if (DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int)) == 0) return true;
        backdrop = DwmSystemBackdropBlur;
        return DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int)) == 0;
    }

    private bool TryApplyAccentAcrylic(System.IntPtr hwnd)
    {
        // Win10 1803+：ACCENT_ENABLE_ACRYLICBLURBEHIND，色调跟随主题（深色近黑 / 浅色近白）。
        var useDark = ThemeManager.Current == AppTheme.Dark;
        var gradient = useDark ? 0x99000000u : 0x99F3F3F3u;
        return SetAccentPolicy(hwnd, AccentEnableAcrylicBlurBehind, gradient);
    }

    private bool SetAccentPolicy(System.IntPtr hwnd, int accentState, uint gradientColor)
    {
        var size = System.Runtime.InteropServices.Marshal.SizeOf<AccentPolicy>();
        var data = new WindowCompositionAttributeData
        {
            Attribute = WcaAccentPolicy,
            SizeOfData = size,
            Data = System.Runtime.InteropServices.Marshal.AllocHGlobal(size)
        };
        try
        {
            var accent = new AccentPolicy { AccentState = accentState, GradientColor = gradientColor };
            System.Runtime.InteropServices.Marshal.StructureToPtr(accent, data.Data, false);
            return SetWindowCompositionAttribute(hwnd, ref data) == 0;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(data.Data);
        }
    }

    private void ResetSystemBackdrop(System.IntPtr hwnd)
    {
        var backdrop = DwmSystemBackdropNone;
        _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
    }

    private void DisableAccentAcrylic(System.IntPtr hwnd) => _ = SetAccentPolicy(hwnd, AccentDisable, 0);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    // ---------- 一体化标题栏（Memory Diary 风格）：WM_GETMINMAXINFO 钳制最大化到工作区 ----------
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    private const int WmGetMinMaxInfo = 0x0024;

    protected override void OnSourceInitialized(System.EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd != System.IntPtr.Zero)
        {
            System.Windows.Interop.HwndSource.FromHwnd(hwnd)?.AddHook(WindowChromeWndProc);
        }
    }

    private System.IntPtr WindowChromeWndProc(System.IntPtr hwnd, int msg, System.IntPtr wParam, System.IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            // WindowStyle=None 最大化时会盖住任务栏：把最大化尺寸/位置钳制到当前监视器工作区
            var mmi = (MinMaxInfo)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(MinMaxInfo))!;
            var area = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
            mmi.MaxPosition.X = area.Left;
            mmi.MaxPosition.Y = area.Top;
            mmi.MaxSize.X = area.Width;
            mmi.MaxSize.Y = area.Height;
            mmi.MaxTrackSize.X = area.Width;
            mmi.MaxTrackSize.Y = area.Height;
            System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
        return System.IntPtr.Zero;
    }

    // ---------- 一体化标题栏窗口控制按钮 ----------
    private void WindowMinimize_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void WindowMaximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
        UpdateWindowCaptionGlyphs();
    }

    private void WindowClose_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateWindowCaptionGlyphs()
    {
        if (WindowMaximizeGlyph is null || WindowRestoreGlyph is null) return;
        var maximized = WindowState == WindowState.Maximized;
        WindowMaximizeGlyph.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        WindowRestoreGlyph.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
        if (WindowMaximizeButton is not null)
        {
            WindowMaximizeButton.ToolTip = maximized ? "还原" : "最大化";
        }
    }

    // 顶栏空白处拖拽移动窗口；双击空白切换最大化/还原；点击按钮（ButtonBase）不拖拽
    private void TopBarSurface_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        if (FindVisualAncestor<System.Windows.Controls.Primitives.ButtonBase>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }
        if (e.ClickCount == 2)
        {
            if (WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(this);
            }
            else
            {
                SystemCommands.MaximizeWindow(this);
            }
            UpdateWindowCaptionGlyphs();
            e.Handled = true;
            return;
        }
        try
        {
            DragMove();
        }
        catch (System.InvalidOperationException)
        {
            // 拖拽期间状态变化等偶发情况，忽略
        }
    }

    // 侧栏顶部 logo 区域拖拽移动窗口
    private void SidebarLogoSurface_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        try
        {
            DragMove();
        }
        catch (System.InvalidOperationException)
        {
        }
    }

    private readonly DispatcherTimer _treemapViewportDebounceTimer;

    public MainWindow()
    {
        _rubberBandAutoScrollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(24)
        };
        _rubberBandAutoScrollTimer.Tick += RubberBandAutoScrollTimer_Tick;
        _treemapViewportDebounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _treemapViewportDebounceTimer.Tick += TreemapViewportDebounceTimer_Tick;
        _windowStateSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _windowStateSaveTimer.Tick += (_, _) => { _windowStateSaveTimer.Stop(); PersistWindowState(); };
        _singleClickPreviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime())
        };
        _singleClickPreviewTimer.Tick += (_, _) => ApplyPendingSingleClick();
        _importTipTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(6)
        };
        _importTipTimer.Tick += ImportTipTimer_Tick;
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
        Activated += (_, _) => _viewModel.RefreshWindowsWallpaper();
        Deactivated += (_, _) => EndRubberBandSelection();
        LocationChanged += (_, _) => ScheduleWindowStateSave();
        StateChanged += (_, _) => ScheduleWindowStateSave();
        StateChanged += (_, _) => UpdateWindowCaptionGlyphs();
        _viewModel.PropertyChanged += MainWindowViewModel_PropertyChanged;
        _viewModel.OpenIndependentViewerRequested += OpenIndependentViewer;
        _viewModel.PeopleAlbums.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PeopleAlbumViewModel.SelectedAlbum))
                RefreshPeopleTabContent();
        };
        _viewModel.TreemapRepopulated += OnTreemapRepopulated;
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, AppTheme theme)
    {
        ApplyTitleBarTheme();
        ApplyWindowMaterial(); // 重新按主题色调应用亚克力（深色/浅色渐变不同）
    }

    private void ImportTipTimer_Tick(object? sender, EventArgs e)
    {
        if (ImportTipText is null)
        {
            return;
        }

        _importTipShowingSecond = !_importTipShowingSecond;
        ImportTipText.Text = _importTipShowingSecond
            ? "💡 无法判断归属时会停下来让你确认，不会乱放。确认目标文件夹后再导入。"
            : "💡 遇到不认识的 RAW / 视频格式？到 设置 → 照片库与导入 → 自定义导入格式 里添加后缀（如 R3D、BRAW），保存后会自动记住。";
    }

    private void ApplyTitleBarTheme()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == System.IntPtr.Zero) return;
            var useDark = ThemeManager.Current == AppTheme.Dark ? 1 : 0;
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
            {
                _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20h1, ref useDark, sizeof(int));
            }
        }
        catch
        {
            // Non-fatal: keep the default system caption if the DWM call fails.
        }
    }

    private void PrimaryNavigationItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _navigationDragStart = e.GetPosition(PrimaryNavigationList);
        _navigationDragSource = (sender as FrameworkElement)?.DataContext as NavigationItemViewModel;
    }

    private void PrimaryNavigationItem_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _navigationDragStart is null || _navigationDragSource is null)
        {
            return;
        }

        var current = e.GetPosition(PrimaryNavigationList);
        if (Math.Abs(current.X - _navigationDragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _navigationDragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var source = _navigationDragSource;
        _navigationDragStart = null;
        _navigationDragSource = null;
        System.Windows.DragDrop.DoDragDrop((DependencyObject)sender, source.Key, System.Windows.DragDropEffects.Move);
    }

    private void PrimaryNavigationList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.StringFormat) || e.Data.GetData(System.Windows.DataFormats.StringFormat) is not string sourceKey)
        {
            return;
        }

        var element = e.OriginalSource as DependencyObject;
        while (element is not null && element is not System.Windows.Controls.Button)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        if (element is System.Windows.Controls.Button { DataContext: NavigationItemViewModel target } targetButton && !string.Equals(sourceKey, target.Key, StringComparison.Ordinal))
        {
            var insertAfter = e.GetPosition(targetButton).Y >= targetButton.ActualHeight / 2;
            _viewModel.MoveNavigationItem(sourceKey, target.Key, insertAfter);
            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        RestoreSafeWindowState();
        ApplyCustomWindowIcon();
        ApplyTitleBarTheme();
        ApplyWindowMaterial();
        _importTipTimer.Start();
        if (App.ScreenshotPage is { } page)
        {
            _viewModel.CurrentPage = page;
        }
        if (App.BrowseShowcaseForScreenshot)
        {
            _viewModel.BrowseDisplayMode = BrowseDisplayMode.Grid;
            _viewModel.IsBrowseConditionsExpanded = true;
        }
        if (App.AdvancedFiltersForScreenshot)
        {
            _viewModel.IsAdvancedFiltersExpanded = true;
        }
        AnimateVisiblePage();
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdatePrimaryNavigationIndicator);
        if (_viewModel.IsPreviewPage)
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ResetGalleryScrollToFirstDate);
        }

        if (App.ScreenshotPath is null && App.ViewerFile is null && _viewModel.HasPendingImportResume)
        {
            PromptResumePendingImport();
        }

        if (App.ScreenshotPath is { } screenshotPath && App.ViewerFile is null)
        {
            // Headless-safe screenshot: let the async library scan and the
            // viewport thumbnail queue settle, then render the window's visual
            // tree to a PNG and exit.
            _ = CaptureScreenshotAfterDelayAsync(screenshotPath);
        }
        if (App.ViewerFile is { } viewerFile)
        {
            OpenViewerForScreenshotOrInspect(viewerFile);
        }
    }

    private async void PromptResumePendingImport()
    {
        var result = System.Windows.MessageBox.Show(
            this,
            "检测到上次未完成的导入。是否继续？\n\n选择「是」继续导入剩余文件；选择「否」放弃上次的导入进度。",
            "继续导入",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await _viewModel.ResumePendingImportAsync();
        }
        else
        {
            _viewModel.DiscardPendingImportResume();
        }
    }

    private void OpenViewerForScreenshotOrInspect(string viewerFile)
    {
        var viewer = new PhotoViewerWindow(new[] { viewerFile }, viewerFile)
        {
            Owner = this
        };
        viewer.Show();
        if (App.ScreenshotPath is { } shot)
        {
            _ = CaptureViewerScreenshotAfterDelayAsync(viewer, shot);
        }
    }

    private static async Task CaptureViewerScreenshotAfterDelayAsync(PhotoViewerWindow viewer, string shot)
    {
        // Let the video start and decode a few frames before grabbing a VLC snapshot.
        await Task.Delay(TimeSpan.FromSeconds(3));
        await viewer.CaptureScreenshotAsync(shot, App.ViewerOverlaysForScreenshot);
    }

    private async Task CaptureScreenshotAfterDelayAsync(string path)
    {
        await Task.Delay(TimeSpan.FromSeconds(8));
        if (App.SelectFirstForScreenshot && _viewModel.PreviewFiles.Count > 0)
        {
            _viewModel.SelectedPreviewFile = _viewModel.PreviewFiles.FirstOrDefault();
            // Give the async metadata read a moment so the Inspector shows populated fields.
            await Task.Delay(TimeSpan.FromMilliseconds(600));
        }
        if (App.SelectFirstPersonForScreenshot && _viewModel.PeopleAlbums.Albums.Count > 0)
        {
            _viewModel.PeopleAlbums.SelectedAlbum = _viewModel.PeopleAlbums.Albums.FirstOrDefault();
            // Let the virtualized photo grid realize its visible tiles and decode thumbnails.
            await Task.Delay(TimeSpan.FromMilliseconds(1200));
        }
        CaptureWindowScreenshot(path);
    }

    private void CaptureWindowScreenshot(string path)
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
            using var stream = System.IO.File.Create(path);
            encoder.Save(stream);
        }
        finally
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        ThemeToggleLabel.Text = ThemeManager.Current == AppTheme.Light ? "深色" : "浅色";
    }

    private void MainWindowViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentPage))
        {
            Dispatcher.BeginInvoke(AnimateVisiblePage);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdatePrimaryNavigationIndicator);
            if (_viewModel.IsPreviewPage)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ResetGalleryScrollToFirstDate);
            }
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.AppIconImage))
        {
            ApplyCustomWindowIcon();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsAcrylicEnabled))
        {
            ApplyWindowMaterial();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ImportSuccessCount))
        {
            AnimateImportCount(ImportSuccessScale);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ImportSkippedCount))
        {
            AnimateImportCount(ImportSkippedScale);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ImportFailedCount))
        {
            AnimateImportCount(ImportFailedScale);
        }
    }

    // 导入摘要数字变化：弹性放大回弹动画（导成功一张数字就跳一下）
    private void AnimateImportCount(ScaleTransform scale)
    {
        if (scale is null)
        {
            return;
        }

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        var storyboard = new Storyboard();
        var bounce = new DoubleAnimation(1.0, 1.28, TimeSpan.FromMilliseconds(110))
        {
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(bounce, scale);
        Storyboard.SetTargetProperty(bounce, new PropertyPath(ScaleTransform.ScaleXProperty));
        storyboard.Children.Add(bounce);

        var bounceY = new DoubleAnimation(1.0, 1.28, TimeSpan.FromMilliseconds(110))
        {
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(bounceY, scale);
        Storyboard.SetTargetProperty(bounceY, new PropertyPath(ScaleTransform.ScaleYProperty));
        storyboard.Children.Add(bounceY);

        storyboard.Begin();
    }

    private void ApplyCustomWindowIcon()
    {
        Icon = _viewModel.AppIconImage ?? DefaultAppIcon;
    }

    private void AnimateVisiblePage()
    {
        var page = ResolveCurrentPage();
        if (page is null)
        {
            return;
        }

        // A fresh transform avoids any in-flight translate animation on the page,
        // and BeginAnimation is inherently interruptible (SnapshotAndReplace).
        var translate = new TranslateTransform();
        page.RenderTransform = translate;
        page.BeginAnimation(UIElement.OpacityProperty, null);

        if (!SystemParameters.ClientAreaAnimation)
        {
            page.Opacity = 1;
            translate.Y = 0;
            return;
        }

        page.Opacity = 0;
        translate.Y = 6;

        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        page.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(6, 0, duration) { EasingFunction = ease });
    }

    private FrameworkElement? ResolveCurrentPage() => _viewModel.CurrentPage switch
    {
        "Home" => HomePage,
        "Import" => ImportPage,
        "Preview" => PreviewPage,
        "CustomAlbums" => CustomAlbumsPageHost,
        "FaceSearch" => FaceSearchPage,
        "MapPhotos" => MapPageHost,
        "Compression" => CompressionPageHost,
        "Watermark" => WatermarkPageHost,
        "Settings" => SettingsCenterPageHost,
        _ => HomePage
    };

    protected override void OnClosed(EventArgs e)
    {
        _windowStateSaveTimer.Stop();
        PersistWindowState();
        _viewModel.PropertyChanged -= MainWindowViewModel_PropertyChanged;
        _viewModel.FaceSearch.Cancel();
        MapPageHost.Dispose();
        base.OnClosed(e);
    }

    private void FaceReference_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void FaceReference_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            await _viewModel.FaceSearch.SetReferenceAsync(files[0]);
        }
        e.Handled = true;
    }

    private void FaceResult_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: FaceSearchResultViewModel item })
        {
            _viewModel.FaceSearch.OpenResultCommand.Execute(item);
            e.Handled = true;
        }
    }

    private void PeopleMainTab_Checked(object sender, RoutedEventArgs e)
    {
        RefreshPeopleTabContent();
    }

    private void RefreshPeopleTabContent()
    {
        // The Checked event also fires while InitializeComponent is still building
        // the visual tree, before the tab panels are assigned to their fields.
        if (PeopleGroupsPanel is null || PeopleGroupsDetail is null || PeopleSearchPanel is null)
        {
            return;
        }

        var showGroups = PeopleTabGroupsButton.IsChecked == true;
        var hasSelection = _viewModel.PeopleAlbums.SelectedAlbum is not null;

        PeopleGroupsPanel.Visibility = showGroups && !hasSelection ? Visibility.Visible : Visibility.Collapsed;
        PeopleGroupsDetail.Visibility = showGroups && hasSelection ? Visibility.Visible : Visibility.Collapsed;
        PeopleSearchPanel.Visibility = showGroups ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PersonPhoto_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: PersonPhotoViewModel item } && System.IO.File.Exists(item.Path))
        {
            // Double click opens inside the app (PhotoViewerWindow), matching
            // the browse-page interaction model. The system default program
            // stays reachable via context menu / Explorer.
            var window = new PhotoViewerWindow(new[] { item.Path }, item.Path, _viewModel.RemoveDeletedViewerPhoto) { Owner = this };
            window.Show();
            e.Handled = true;
        }
    }

    private void PersonPhoto_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PersonPhotoViewModel photo })
        {
            photo.EnsureThumbnailLoaded();
        }
    }

    private void LibraryDates_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is LibraryDateNode node)
        {
            _viewModel.SelectedDate = node;
        }
    }

    private void PreviewScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var galleryPanel = GetGalleryPanel();
        if (galleryPanel is null)
        {
            return;
        }

        var tileSize = GalleryZoomPolicy.ResolveWheelTileSize(
            _viewModel.ZoomableGridTileSize,
            e.Delta,
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
        if (tileSize is null)
        {
            galleryPanel.SetVerticalOffset(galleryPanel.VerticalOffset - e.Delta);
            e.Handled = true;
            return;
        }

        ApplyGalleryZoom(tileSize.Value, e.GetPosition(PreviewPhotoScrollViewer));
        e.Handled = true;
    }

    private void PrimaryNavigationHost_SizeChanged(object sender, SizeChangedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdatePrimaryNavigationIndicator);

    private void UpdatePrimaryNavigationIndicator()
    {
        if (PrimaryNavigationHost is null || PrimaryNavigationSelectionIndicator is null ||
            PrimaryNavigationSelectionTransform is null)
        {
            return;
        }

        var selectedButton = FindVisualDescendants<System.Windows.Controls.Button>(PrimaryNavigationList)
            .FirstOrDefault(button => button.DataContext is NavigationItemViewModel item &&
                                      string.Equals(item.Key, _viewModel.CurrentPage, StringComparison.Ordinal));
        if (selectedButton is null || selectedButton.ActualHeight <= 0)
        {
            PrimaryNavigationSelectionIndicator.BeginAnimation(UIElement.OpacityProperty, null);
            PrimaryNavigationSelectionIndicator.Opacity = 0;
            return;
        }

        var target = selectedButton.TranslatePoint(new System.Windows.Point(0, 0), PrimaryNavigationHost);
        PrimaryNavigationSelectionIndicator.Height = selectedButton.ActualHeight;
        var duration = FindResource("Motion.Duration.Normal") is Duration motionDuration && motionDuration.HasTimeSpan
            ? motionDuration.TimeSpan
            : TimeSpan.FromMilliseconds(180);
        var easing = FindResource("Motion.Easing.Standard") as IEasingFunction;

        if (PrimaryNavigationSelectionIndicator.Opacity < 0.01)
        {
            PrimaryNavigationSelectionTransform.BeginAnimation(TranslateTransform.YProperty, null);
            PrimaryNavigationSelectionTransform.Y = target.Y;
            PrimaryNavigationSelectionIndicator.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
            return;
        }

        var slide = new DoubleAnimation(PrimaryNavigationSelectionTransform.Y, target.Y, duration)
        {
            EasingFunction = easing
        };
        PrimaryNavigationSelectionTransform.BeginAnimation(TranslateTransform.YProperty, slide, HandoffBehavior.SnapshotAndReplace);
    }

    private void GalleryZoomOut_Click(object sender, RoutedEventArgs e) =>
        ApplyGalleryZoom(
            _viewModel.ZoomableGridTileSize / GalleryZoomPolicy.WheelNotchFactor,
            GalleryViewportCenter());

    private void GalleryZoomIn_Click(object sender, RoutedEventArgs e) =>
        ApplyGalleryZoom(
            _viewModel.ZoomableGridTileSize * GalleryZoomPolicy.WheelNotchFactor,
            GalleryViewportCenter());

    private void GalleryZoomReset_Click(object sender, RoutedEventArgs e) =>
        ApplyGalleryZoom(GalleryZoomPolicy.DefaultTileSize, GalleryViewportCenter());

    private void GalleryZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || Math.Abs(e.NewValue - _viewModel.ZoomableGridTileSize) < 0.01)
        {
            return;
        }

        ApplyGalleryZoom(e.NewValue, GalleryViewportCenter());
    }

    private System.Windows.Point GalleryViewportCenter() => new(
        PreviewPhotoScrollViewer.ViewportWidth / 2,
        PreviewPhotoScrollViewer.ViewportHeight / 2);

    private HanabePhotoManager.App.Controls.VirtualizingWrapPanel? GetGalleryPanel() =>
        FindVisualDescendants<HanabePhotoManager.App.Controls.VirtualizingWrapPanel>(PreviewWallItemsControl)
            .FirstOrDefault();

    private void ResetGalleryScrollToFirstDate()
    {
        GetGalleryPanel()?.SetVerticalOffset(0);
    }

    private void ApplyGalleryZoom(double requestedTileSize, System.Windows.Point anchor)
    {
        var galleryPanel = GetGalleryPanel();
        if (galleryPanel is null)
        {
            return;
        }

        var oldTileSize = _viewModel.ZoomableGridTileSize;
        var newTileSize = Math.Clamp(
            requestedTileSize,
            GalleryZoomPolicy.MinimumTileSize,
            GalleryZoomPolicy.MaximumTileSize);
        if (Math.Abs(newTileSize - oldTileSize) < 0.01)
        {
            return;
        }

        var requestedOffset = GalleryZoomPolicy.CalculateAnchoredVerticalOffset(
            galleryPanel.VerticalOffset,
            anchor.X,
            anchor.Y,
            galleryPanel.ViewportWidth,
            oldTileSize + GalleryZoomPolicy.TileSpacing,
            newTileSize + GalleryZoomPolicy.TileSpacing,
            GalleryZoomPolicy.HeaderHeight,
            double.MaxValue);

        var generation = ++_galleryZoomGeneration;
        _viewModel.ZoomableGridTileSize = newTileSize;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (generation != _galleryZoomGeneration)
            {
                return;
            }

            galleryPanel.UpdateLayout();
            galleryPanel.SetVerticalOffset(Math.Clamp(
                requestedOffset,
                0,
                Math.Max(0, galleryPanel.ExtentHeight - galleryPanel.ViewportHeight)));
        });
    }

    private void TreemapScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        var scrollViewer = TreemapScrollViewer;
        var pointer = e.GetPosition(scrollViewer);
        var oldZoom = _viewModel.TreemapZoom;
        var factor = e.Delta > 0 ? TreemapZoomNotchFactor : 1.0 / TreemapZoomNotchFactor;
        var newZoom = Math.Clamp(oldZoom * factor, TreemapZoomMin, TreemapZoomMax);
        if (Math.Abs(newZoom - oldZoom) < 0.01)
        {
            e.Handled = true;
            return;
        }

        // 画布放大：整个节点布局按比例缩放，锚点 = 鼠标下的内容点（scale 1.0 坐标）。
        var anchorContentX = (scrollViewer.HorizontalOffset + pointer.X) / oldZoom;
        var anchorContentY = (scrollViewer.VerticalOffset + pointer.Y) / oldZoom;
        AnimateTreemapZoom(newZoom, anchorContentX, anchorContentY, pointer.X, pointer.Y);

        e.Handled = true;
    }

    /// <summary>
    /// 画布级平滑缩放：围绕鼠标（或视口中心）把整个节点布局按比例放大/缩小，
    /// 180ms 三次缓出过渡。只缩放画布整体，不触发任何节点级动画。
    /// </summary>
    private void AnimateTreemapZoom(
        double toZoom,
        double anchorContentX,
        double anchorContentY,
        double anchorViewportX,
        double anchorViewportY)
    {
        if (TreemapControl is null || TreemapScrollViewer is null)
        {
            return;
        }

        _treemapZoomAnimation?.Stop();
        _treemapZoomAnimation = null;

        var fromZoom = _viewModel?.TreemapZoom ?? 1.0;
        if (Math.Abs(fromZoom - toZoom) < 0.001)
        {
            ApplyTreemapZoom(toZoom, anchorContentX, anchorContentY, anchorViewportX, anchorViewportY);
            return;
        }

        var started = DateTime.UtcNow;
        var duration = TimeSpan.FromMilliseconds(180);
        var timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };

        timer.Tick += (_, _) =>
        {
            var progress = Math.Clamp((DateTime.UtcNow - started).TotalMilliseconds / duration.TotalMilliseconds, 0d, 1d);
            var eased = 1d - Math.Pow(1d - progress, 3d); // cubic ease-out
            var zoom = fromZoom + (toZoom - fromZoom) * eased;
            ApplyTreemapZoom(zoom, anchorContentX, anchorContentY, anchorViewportX, anchorViewportY);

            if (progress >= 1d)
            {
                timer.Stop();
                if (ReferenceEquals(_treemapZoomAnimation, timer))
                {
                    _treemapZoomAnimation = null;
                }
            }
        };

        _treemapZoomAnimation = timer;
        timer.Start();
    }

    private void ApplyTreemapZoom(
        double zoom,
        double anchorContentX,
        double anchorContentY,
        double anchorViewportX,
        double anchorViewportY)
    {
        if (_viewModel is null || TreemapControl is null || TreemapScrollViewer is null)
        {
            return;
        }

        _viewModel.TreemapZoom = zoom;
        UpdateTreemapSize();
        TreemapScrollViewer.UpdateLayout();

        // 锚点内容点始终保持在视口锚点位置，实现围绕鼠标的平滑缩放。
        TreemapScrollViewer.ScrollToHorizontalOffset(Math.Clamp(anchorContentX * zoom - anchorViewportX, 0, TreemapScrollViewer.ScrollableWidth));
        TreemapScrollViewer.ScrollToVerticalOffset(Math.Clamp(anchorContentY * zoom - anchorViewportY, 0, TreemapScrollViewer.ScrollableHeight));
    }

    private void TreemapScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTreemapSize();
    }

    private void TreemapScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        SyncTreemapVisibleRect();
        ScheduleTreemapViewportLoad();
    }

    private void UpdateTreemapSize()
    {
        if (TreemapControl is null || TreemapScrollViewer is null)
        {
            return;
        }

        var zoom = _viewModel?.TreemapZoom ?? 1.0;

        // Use LayoutTransform so ScrollViewer sees the scaled size for extent/scroll.
        // This makes zoom < 1.0 (zoom out) actually shrink the control visually.
        TreemapControl.LayoutTransform = new ScaleTransform(zoom, zoom);

        // The control's own Width/Height stay at content-native size.
        // ScrollViewer extent = content * zoom  (via LayoutTransform).
        var isPanorama = PhotoTreemapControl.IsPanoramaZoom(zoom);
        var isRootOverview = _viewModel?.TreemapBrowser.CurrentContainerKey is null;
        var panorama = isPanorama
            ? TreemapControl.GetPanoramaLayout(TreemapScrollViewer.ViewportWidth)
            : null;
        var cw = panorama?.ContentWidth ?? (isRootOverview ? TreemapScrollViewer.ViewportWidth : TreemapControl.ContentWidth);
        var ch = panorama?.ContentHeight ?? (isRootOverview ? TreemapScrollViewer.ViewportHeight : TreemapControl.ContentHeight);
        TreemapControl.Width = Math.Max(cw, 1);
        TreemapControl.Height = Math.Max(ch, 1);

        TreemapControl.InvalidateVisual();
        SyncTreemapVisibleRect();
        ScheduleTreemapViewportLoad();
    }

    private void SyncTreemapVisibleRect()
    {
        if (TreemapControl is null || TreemapScrollViewer is null)
        {
            return;
        }

        var zoom = Math.Max(_viewModel?.TreemapZoom ?? 1.0, TreemapZoomMin);
        TreemapControl.VisibleRect = new Rect(
            TreemapScrollViewer.HorizontalOffset / zoom,
            TreemapScrollViewer.VerticalOffset / zoom,
            TreemapScrollViewer.ViewportWidth / zoom,
            TreemapScrollViewer.ViewportHeight / zoom);
    }

    private void OnTreemapRepopulated()
    {
        UpdateTreemapSize();
        SyncTreemapVisibleRect();
        TreemapControl?.InvalidateVisual();
        ScheduleTreemapViewportLoad();
        // Root already uses viewport bounds; subtrees retain their content fit.
        if (_viewModel?.TreemapBrowser.CurrentContainerKey is not null)
        {
            DeferFitTreemapToView();
        }
    }

    /// <summary>
    /// After the treemap has been laid out, schedule a one-shot fit-to-view.
    /// The debounce timer fires once the layout has settled (content dimensions
    /// are known) and the ScrollViewer is ready.
    /// </summary>
    private void DeferFitTreemapToView()
    {
        _treemapFitPending = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_treemapFitPending || TreemapControl is null || TreemapScrollViewer is null) return;
            _treemapFitPending = false;

            var cw = TreemapControl.ContentWidth;
            var ch = TreemapControl.ContentHeight;
            if (cw <= 1 || ch <= 1) return;

            var vpW = TreemapScrollViewer.ViewportWidth;
            var vpH = TreemapScrollViewer.ViewportHeight;
            var margin = 16.0;
            var availableW = vpW - margin * 2;
            var availableH = vpH - margin * 2;

            var fitZoom = Math.Min(1.0, Math.Min(availableW / cw, availableH / ch));
            fitZoom = Math.Max(fitZoom, 0.02);

            _viewModel.TreemapZoom = fitZoom;
            UpdateTreemapSize();
            TreemapScrollViewer.ScrollToHorizontalOffset(0);
            TreemapScrollViewer.ScrollToVerticalOffset(0);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private bool _treemapFitPending;

    private void ScheduleTreemapViewportLoad()
    {
        if (_treemapViewportDebounceTimer is null) return;
        _treemapViewportDebounceTimer.Stop();
        _treemapViewportDebounceTimer.Start();
    }

    private void TreemapViewportDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _treemapViewportDebounceTimer.Stop();
        if (TreemapControl is null || !_viewModel.IsTreemapBrowseMode) return;
        _viewModel.RefreshTreemapViewportLoading(TreemapControl.VisibleItemPathsNeedingThumbnail);
    }

    private void FileTypeFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        var typeGroup = btn.Content?.ToString() ?? "全部";
        _viewModel.ToggleFileTypeFilter(typeGroup);
    }
    private void TreemapControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTreemapSize();
        TreemapControl?.InvalidateVisual();
    }

    private void PreviewScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        // Space + left drag pan for treemap
        if (_isSpaceHeld && e.ChangedButton == MouseButton.Left &&
            _viewModel.IsTreemapBrowseMode &&
            !IsTextInputFocused())
        {
            _isSpacePanning = true;
            _spacePanStartPoint = e.GetPosition(scrollViewer);
            _spacePanStartVerticalOffset = scrollViewer.VerticalOffset;
            _spacePanStartHorizontalOffset = scrollViewer.HorizontalOffset;
            scrollViewer.Cursor = System.Windows.Input.Cursors.ScrollAll;
            scrollViewer.CaptureMouse();
            e.Handled = true;
            return;
        }

        // Middle-button pan (existing)
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isGridPanning = true;
        _gridPanStartPoint = e.GetPosition(scrollViewer);
        _gridPanStartVerticalOffset = scrollViewer.VerticalOffset;
        _gridPanStartHorizontalOffset = scrollViewer.HorizontalOffset;
        scrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void PreviewScrollViewer_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        // Handle space panning
        if (_isSpacePanning)
        {
            var current = e.GetPosition(scrollViewer);
            var deltaY = current.Y - _spacePanStartPoint.Y;
            var deltaX = current.X - _spacePanStartPoint.X;
            scrollViewer.ScrollToVerticalOffset(Math.Clamp(
                _spacePanStartVerticalOffset - deltaY, 0, scrollViewer.ScrollableHeight));
            scrollViewer.ScrollToHorizontalOffset(Math.Clamp(
                _spacePanStartHorizontalOffset - deltaX, 0, scrollViewer.ScrollableWidth));
            e.Handled = true;
            return;
        }

        // Handle middle-button panning
        if (!_isGridPanning)
        {
            return;
        }

        var currentM = e.GetPosition(scrollViewer);
        var deltaYM = currentM.Y - _gridPanStartPoint.Y;
        var deltaXM = currentM.X - _gridPanStartPoint.X;
        scrollViewer.ScrollToVerticalOffset(Math.Clamp(
            _gridPanStartVerticalOffset - deltaYM, 0, scrollViewer.ScrollableHeight));
        scrollViewer.ScrollToHorizontalOffset(Math.Clamp(
            _gridPanStartHorizontalOffset - deltaXM, 0, scrollViewer.ScrollableWidth));
        e.Handled = true;
    }

    private void PreviewScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        // Space-pan release
        if (_isSpacePanning && e.ChangedButton == MouseButton.Left)
        {
            _isSpacePanning = false;
            if (sender is ScrollViewer spaceSv && spaceSv.IsMouseCaptured)
            {
                spaceSv.ReleaseMouseCapture();
            }

            if (sender is ScrollViewer spaceSvCursor)
            {
                spaceSvCursor.Cursor = _isSpaceHeld ? System.Windows.Input.Cursors.ScrollAll : System.Windows.Input.Cursors.Arrow;
            }

            ScheduleTreemapViewportLoad();
            e.Handled = true;
            return;
        }

        // Middle-button pan release
        if (e.ChangedButton != MouseButton.Middle || !_isGridPanning)
        {
            return;
        }

        _isGridPanning = false;
        if (sender is ScrollViewer scrollViewer && scrollViewer.IsMouseCaptured)
        {
            scrollViewer.ReleaseMouseCapture();
        }

        ScheduleTreemapViewportLoad();
        e.Handled = true;
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.UpdateResponsiveBrowseLayout(e.NewSize.Width, e.NewSize.Height);
        ScheduleWindowStateSave();
    }

    private void ScheduleWindowStateSave()
    {
        if (!IsLoaded) return;
        _windowStateSaveTimer.Stop();
        _windowStateSaveTimer.Start();
    }

    private void PersistWindowState()
    {
        var bounds = RestoreBounds;
        _viewModel.RememberWindowState(bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState.ToString());
    }

    private void RestoreSafeWindowState()
    {
        var area = SystemParameters.WorkArea;
        var width = Math.Clamp(_viewModel.WindowWidth, MinWidth, area.Width);
        var height = Math.Clamp(_viewModel.WindowHeight, MinHeight, area.Height);
        var left = _viewModel.RestoreWindowState && _viewModel.WindowLeft is { } savedLeft ? savedLeft : area.Left + (area.Width - width) / 2;
        var top = _viewModel.RestoreWindowState && _viewModel.WindowTop is { } savedTop ? savedTop : area.Top + (area.Height - height) / 2;
        Left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - width));
        Top = Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - height));
        Width = width;
        Height = height;
        if (_viewModel.RestoreWindowState && _viewModel.SavedWindowState == nameof(WindowState.Maximized)) WindowState = WindowState.Maximized;
    }

    private async void EditedDropTarget_Drop(object sender, System.Windows.DragEventArgs e)
    {
        await HandleDropAsync(e, MediaCategory.Edited);
    }

    private async void MaterialDropTarget_Drop(object sender, System.Windows.DragEventArgs e)
    {
        await HandleDropAsync(e, MediaCategory.Material);
    }

    private void SourceAutoImportDropTarget_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void SourceAutoImportDropTarget_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var paths = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop) ?? Array.Empty<string>();
            await _viewModel.AutoImportDroppedSourceAsync(paths);
            e.Handled = true;
        }
    }

    private async Task HandleDropAsync(System.Windows.DragEventArgs e, MediaCategory category)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var paths = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop) ?? Array.Empty<string>();
            await _viewModel.ImportLooseFilesAsync(paths, category);
        }
    }

    private void CloseExifPanel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _viewModel.SelectedPreviewFile = null;
    }

    private void Inspector_Open(object sender, RoutedEventArgs e)
    {
        var file = _viewModel.SelectedPreviewFile;
        if (file is not null && System.IO.File.Exists(file.PreviewPath))
        {
            OpenIndependentViewer(file);
        }
    }

    private void Inspector_OpenFolder(object sender, RoutedEventArgs e)
    {
        var file = _viewModel.SelectedPreviewFile;
        if (file is not null && System.IO.File.Exists(file.PreviewPath))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file.PreviewPath}\"");
        }
    }

    private void Inspector_CopyPath(object sender, RoutedEventArgs e)
    {
        var file = _viewModel.SelectedPreviewFile;
        if (file is not null)
        {
            System.Windows.Clipboard.SetText(file.PreviewPath);
            _viewModel.StatusMessage = $"已复制：{file.PreviewPath}";
        }
    }

    private void Inspector_Delete(object sender, RoutedEventArgs e)
    {
        var file = _viewModel.SelectedPreviewFile;
        if (file is not null) _viewModel.DeletePreviewFile(file);
    }

    private void Inspector_BatchCopy(object sender, RoutedEventArgs e) => BatchCopySelected();

    private void Inspector_BatchMove(object sender, RoutedEventArgs e) => BatchMoveSelected();

    private void Inspector_ClearSelection(object sender, RoutedEventArgs e) => ClearPreviewSelection();

    private static PreviewFileViewModel? GetFileFromSender(object? sender) =>
        (sender as System.Windows.FrameworkElement)?.DataContext as PreviewFileViewModel;

    private void PreviewContextMenu_Rate5(object sender, RoutedEventArgs e) { SetRating(e, 5); }
    private void PreviewContextMenu_Rate4(object sender, RoutedEventArgs e) { SetRating(e, 4); }
    private void PreviewContextMenu_Rate3(object sender, RoutedEventArgs e) { SetRating(e, 3); }
    private void PreviewContextMenu_Rate2(object sender, RoutedEventArgs e) { SetRating(e, 2); }
    private void PreviewContextMenu_Rate1(object sender, RoutedEventArgs e) { SetRating(e, 1); }
    private void PreviewContextMenu_Rate0(object sender, RoutedEventArgs e) { SetRating(e, 0); }

    private void SetRating(RoutedEventArgs e, int rating)
    {
        var file = _viewModel.SelectedPreviewFile;
        if (file is not null)
        {
            file.Rating = rating;
            var stars = rating > 0 ? new string('★', rating) : "✕ 删除标记";
            _viewModel.StatusMessage = $"已评分：{file.Name} → {stars}";
        }
    }

    private void PreviewContextMenu_TagPortrait(object sender, RoutedEventArgs e) => SetTag("人像");
    private void PreviewContextMenu_TagLandscape(object sender, RoutedEventArgs e) => SetTag("风光");
    private void PreviewContextMenu_TagWaste(object sender, RoutedEventArgs e) => SetTag("废片");
    private void PreviewContextMenu_ClearTag(object sender, RoutedEventArgs e) => SetTag("");

    private void SetTag(string tag)
    {
        var file = _viewModel.SelectedPreviewFile;
        if (file is not null)
        {
            file.Tags = tag;
            _viewModel.StatusMessage = string.IsNullOrWhiteSpace(tag)
                ? $"已清除标签：{file.Name}"
                : $"已标记：{file.Name} → {tag}";
        }
    }

    private void PreviewContextMenu_Delete(object sender, RoutedEventArgs e)
    {
        var file = _viewModel.SelectedPreviewFile;
        if (file is not null) _viewModel.DeletePreviewFile(file);
    }

    private void PreviewSelection_Changed(object sender, RoutedEventArgs e) =>
        _viewModel.NotifyPreviewSelectionChanged();

    /// <summary>
    /// 展开日期分组后新加入照片墙的瓷砖播放一次「向下 reveal」动画：以顶部为锚点
    /// 纵向展开（ScaleY 0→1）+ 淡入，并逐项错峰，形成内容从日期标题区域向下连续展开的
    /// 效果。只对标记了 <see cref="PreviewFileViewModel.RevealOnLoad"/> 的瓷砖触发，
    /// 滚动进入视口时不会重复播放。
    /// </summary>
    private void PreviewWallTile_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.FrameworkElement { DataContext: PreviewFileViewModel item } element)
        {
            return;
        }

        UpdatePreviewCardClip(element);
        if (!item.RevealOnLoad) return;

        item.RevealOnLoad = false;

        var scale = new ScaleTransform(1.0, 0.0);
        element.RenderTransformOrigin = new System.Windows.Point(0.5, 0);
        element.RenderTransform = scale;
        element.Opacity = 0;

        var duration = TimeSpan.FromMilliseconds(240);
        var beginTime = TimeSpan.FromMilliseconds(item.RevealDelayMs);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var scaleY = new DoubleAnimation(0.0, 1.0, duration)
        {
            BeginTime = beginTime,
            EasingFunction = easing
        };
        var opacity = new DoubleAnimation(0, 1, duration)
        {
            BeginTime = beginTime,
            EasingFunction = easing
        };

        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        element.BeginAnimation(UIElement.OpacityProperty, opacity);
    }

    private void PreviewWallTile_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            UpdatePreviewCardClip(element);
        }
    }

    private static void UpdatePreviewCardClip(FrameworkElement element)
    {
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return;
        }

        var radius = System.Windows.Application.Current.TryFindResource("Radius.Card") is CornerRadius cornerRadius
            ? cornerRadius.TopLeft
            : 12d;
        element.Clip = new RectangleGeometry(
            new Rect(0, 0, element.ActualWidth, element.ActualHeight),
            radius,
            radius);
    }

    private void PreviewContextMenu_BatchCopy(object sender, RoutedEventArgs e) => BatchCopySelected();
    private void PreviewContextMenu_BatchMove(object sender, RoutedEventArgs e) => BatchMoveSelected();

    private void BatchCopySelected()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择目标文件夹" };
        if (dlg.ShowDialog() != true) return;
        _viewModel.BatchCopyFilesTo(dlg.FolderName);
    }

    private void BatchMoveSelected()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择目标文件夹" };
        if (dlg.ShowDialog() != true) return;
        _viewModel.BatchMoveFilesTo(dlg.FolderName);
    }

    private void PreviewContextMenu_BatchDelete(object sender, RoutedEventArgs e)
    {
        DeleteSelectedFiles();
    }

    private void DeleteSelectedFiles()
    {
        _viewModel.DeleteSelectedFilesCommand.Execute(null);
    }

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Space = enter canvas drag mode (treemap only, not in text fields)
        if (e.Key == Key.Space &&
            _viewModel.IsTreemapBrowseMode &&
            !IsTextInputFocused() &&
            !e.IsRepeat)
        {
            if (!_isSpaceHeld)
            {
                _isSpaceHeld = true;
                TreemapScrollViewer.Cursor = System.Windows.Input.Cursors.ScrollAll;
                e.Handled = true;
            }

            return;
        }

        // Ctrl+F = jump to the smart search box on the browse page.
        if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && _viewModel.IsPreviewPage)
        {
            FocusBrowseSearch();
            e.Handled = true;
            return;
        }

        // Ctrl+1..9 = switch to the Nth navigation page (current sidebar order).
        if (e.Key >= Key.D1 && e.Key <= Key.D9 && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            var index = e.Key - Key.D1;
            var items = _viewModel.NavigationItems;
            if (index < items.Count && items[index].Command.CanExecute(null))
            {
                items[index].Command.Execute(null);
                e.Handled = true;
            }

            return;
        }

        var file = _viewModel.SelectedPreviewFile;
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
        {
            return;
        }

        if (_viewModel.PhotoViewer.IsOpen)
        {
            // Ctrl 组合键（如 Ctrl+1..9 切换页面）已在上方处理，这里只响应纯方向/数字键。
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;

            if (e.Key is Key.Left or Key.Up) _viewModel.PhotoViewer.Previous();
            else if (e.Key is Key.Right or Key.Down) _viewModel.PhotoViewer.Next();
            else if (e.Key == Key.Escape) _viewModel.PhotoViewer.Close();
            else if (e.Key == Key.Delete) _viewModel.PhotoViewer.DeleteCurrent();
            else if (e.Key >= Key.D0 && e.Key <= Key.D5) _viewModel.PhotoViewer.SetRating(e.Key - Key.D0);
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad5) _viewModel.PhotoViewer.SetRating(e.Key - Key.NumPad0);
            else return;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && _viewModel.IsPreviewPage)
        {
            foreach (var item in _viewModel.PreviewFiles) item.IsSelected = false;
            foreach (var item in _viewModel.VisiblePreviewFiles) item.IsSelected = true;
            _viewModel.NotifyPreviewSelectionChanged();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _viewModel.IsPreviewPage)
        {
            ClearPreviewSelection();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Delete && _viewModel.HasSelectedFiles)
        {
            _viewModel.DeleteSelectedFilesCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Delete && file is not null)
        {
            PreviewContextMenu_Delete(sender, e);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.S && file is not null)
        {
            var next = (file.Rating + 1) % 6; // cycle 0→1→2→3→4→5→0
            file.Rating = next;
            _viewModel.StatusMessage = $"评分：{file.Name} → {(next == 0 ? "✕" : new string('★', next))}";
            e.Handled = true;
        }
        else if (e.Key >= System.Windows.Input.Key.D0 && e.Key <= System.Windows.Input.Key.D5 && file is not null)
        {
            var num = e.Key - System.Windows.Input.Key.D0;
            file.Rating = num;
            _viewModel.StatusMessage = $"评分：{file.Name} → {(num == 0 ? "✕ 删除标记" : new string('★', num))}";
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Enter && file is not null
                 && Keyboard.FocusedElement is not System.Windows.Controls.Primitives.ButtonBase
                 && System.IO.File.Exists(file.PreviewPath))
        {
            // 回车：打开选中照片（独立查看器窗口），与双击行为一致。
            OpenIndependentViewer(file);
            e.Handled = true;
        }
    }

    private void MainWindow_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Space && _isSpaceHeld)
        {
            _isSpaceHeld = false;
            if (!_isSpacePanning)
            {
                TreemapScrollViewer.Cursor = System.Windows.Input.Cursors.Arrow;
            }

            e.Handled = true;
        }
    }

    private static bool IsTextInputFocused()
    {
        return Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase ||
               Keyboard.FocusedElement is System.Windows.Controls.ComboBox;
    }

    private void FocusBrowseSearch()
    {
        _viewModel.IsBrowseConditionsExpanded = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            BrowseSmartSearchBox.Focus();
            BrowseSmartSearchBox.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void PreviewThumbnail_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement element
            && element.DataContext is PreviewFileViewModel file)
        {
            if (FindVisualAncestor<System.Windows.Controls.CheckBox>(e.OriginalSource as DependencyObject) is not null)
            {
                // The selection checkbox handles its own toggle. A pending
                // single-click preview from a previous card must not fire over
                // the checkbox interaction.
                CancelPendingSingleClick();
                return;
            }

            // Double click: open inside the app (PhotoViewerWindow). It is
            // mutually exclusive with the single-click preview — no selection
            // or Inspector side effect runs first.
            if (e.ClickCount == 2)
            {
                CancelPendingSingleClick();
                if (System.IO.File.Exists(file.PreviewPath))
                {
                    OpenIndependentViewer(file);
                }
                e.Handled = true;
                return;
            }

            // Single click: defer the preview until a double click can be
            // ruled out. Shift/Ctrl modifiers are captured at press time.
            var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            _singleClickPreviewTimer.Stop();
            _pendingSingleClickFile = file;
            _pendingSingleClickControl = control;
            _pendingSingleClickShift = shift;
            _singleClickPreviewTimer.Start();
        }
    }

    private void CancelPendingSingleClick()
    {
        _singleClickPreviewTimer.Stop();
        _pendingSingleClickFile = null;
    }

    private void ApplyPendingSingleClick()
    {
        _singleClickPreviewTimer.Stop();
        var file = _pendingSingleClickFile;
        _pendingSingleClickFile = null;
        if (file is null)
        {
            return;
        }

        _viewModel.SelectedPreviewFile = file;
        var control = _pendingSingleClickControl;
        var shift = _pendingSingleClickShift;

        // Shift: range selection (additive with Ctrl). Ctrl: toggle selection.
        if (shift)
        {
            SelectPreviewRange(file, additive: control);
        }
        else if (control)
        {
            file.IsSelected = !file.IsSelected;
            _selectionAnchor = file;
        }
        else if (_viewModel.IsMultiSelectMode)
        {
            // Manual multi-select mode: plain single clicks toggle the checkbox.
            file.IsSelected = !file.IsSelected;
            _selectionAnchor = file;
        }
        else
        {
            // Single click outside multi-select mode: preview only. Never
            // auto-checks the selection box, never enters multi-select.
            foreach (var item in _viewModel.PreviewFiles) item.IsSelected = false;
            _selectionAnchor = file;
        }

        _viewModel.NotifyPreviewSelectionChanged();
    }

    private void SelectPreviewRange(PreviewFileViewModel clicked, bool additive)
    {
        var items = _viewModel.VisiblePreviewFiles;
        var clickedIndex = items.IndexOf(clicked);
        var anchorIndex = _selectionAnchor is null ? -1 : items.IndexOf(_selectionAnchor);
        if (clickedIndex < 0) return;
        if (anchorIndex < 0) anchorIndex = clickedIndex;

        if (!additive)
        {
            foreach (var item in _viewModel.PreviewFiles) item.IsSelected = false;
        }

        var first = Math.Min(anchorIndex, clickedIndex);
        var last = Math.Max(anchorIndex, clickedIndex);
        for (var index = first; index <= last; index++) items[index].IsSelected = true;
    }

    private void ClearPreviewSelection()
    {
        EndRubberBandSelection();
        foreach (var item in _viewModel.PreviewFiles) item.IsSelected = false;
        _selectionAnchor = null;
        _viewModel.NotifyPreviewSelectionChanged();
    }

    private void PreviewSelectionSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            FindPreviewCard(e.OriginalSource as DependencyObject) is not null ||
            FindVisualAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null ||
            FindVisualAncestor<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        // Starting a rubber-band drag supersedes any pending single-click preview.
        CancelPendingSingleClick();
        _rubberBandStart = e.GetPosition(PreviewSelectionSurface);
        _rubberBandBaseline = _viewModel.VisiblePreviewFiles.ToDictionary(item => item, item => item.IsSelected);
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            foreach (var item in _viewModel.PreviewFiles) item.IsSelected = false;
        }
        PreviewSelectionSurface.CaptureMouse();
        e.Handled = true;
    }

    private void PreviewSelectionSurface_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_rubberBandStart is not { } start || e.LeftButton != MouseButtonState.Pressed) return;

        var current = e.GetPosition(PreviewSelectionSurface);
        if (PreviewSelectionRectangle.Visibility != Visibility.Visible &&
            Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        UpdateRubberBandSelection(start, current);
        if (!_rubberBandAutoScrollTimer.IsEnabled) _rubberBandAutoScrollTimer.Start();
        e.Handled = true;
    }

    private void UpdateRubberBandSelection(System.Windows.Point start, System.Windows.Point current)
    {
        var selection = new Rect(start, current);
        PreviewSelectionRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(PreviewSelectionRectangle, selection.Left);
        Canvas.SetTop(PreviewSelectionRectangle, selection.Top);
        PreviewSelectionRectangle.Width = selection.Width;
        PreviewSelectionRectangle.Height = selection.Height;

        var toggle = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        // Virtualized wall: hit-test against the whole item set through the
        // panel's row table so cards outside the realized window (which are not
        // in the visual tree at all) still participate in the rubber band.
        // The panel reports bounds in its own viewport-relative coordinates, so
        // translate them to the selection surface before intersecting.
        var wallPanel = FindVisualDescendants<HanabePhotoManager.App.Controls.VirtualizingWrapPanel>(PreviewWallItemsControl)
            .FirstOrDefault();
        var wallItems = _viewModel.PreviewWallItems;
        var handled = new HashSet<PreviewFileViewModel>();
        if (wallPanel is not null)
        {
            var panelOrigin = wallPanel.TranslatePoint(new System.Windows.Point(0, 0), PreviewSelectionSurface);
            foreach (var (itemIndex, bounds, isHeader) in wallPanel.GetItemBounds())
            {
                if (isHeader || itemIndex < 0 || itemIndex >= wallItems.Count) continue;
                if (wallItems[itemIndex] is not PreviewFileViewModel item) continue;
                handled.Add(item);
                var surfaceBounds = new Rect(
                    bounds.X + panelOrigin.X,
                    bounds.Y + panelOrigin.Y,
                    bounds.Width,
                    bounds.Height);
                ApplyRubberBandHit(item, selection, surfaceBounds, toggle);
            }
        }

        // Fallback for realized cards the row table did not cover (e.g. the
        // panel has not laid out yet): keep the previous visual-tree hit test.
        foreach (var card in FindVisualDescendants<FrameworkElement>(PreviewSelectionSurface)
                     .Where(element => Equals(element.Tag, "PreviewCard")))
        {
            if (card.DataContext is not PreviewFileViewModel item || !handled.Add(item)) continue;
            var bounds = card.TransformToAncestor(PreviewSelectionSurface)
                .TransformBounds(new Rect(new System.Windows.Point(0, 0), card.RenderSize));
            ApplyRubberBandHit(item, selection, bounds, toggle);
        }

        _viewModel.NotifyPreviewSelectionChanged();
    }

    private void ApplyRubberBandHit(PreviewFileViewModel item, Rect selection, Rect bounds, bool toggle)
    {
        var hit = selection.IntersectsWith(bounds);
        var baseline = _rubberBandBaseline.GetValueOrDefault(item);
        item.IsSelected = toggle ? hit != baseline : hit;
    }

    private void PreviewSelectionSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_rubberBandStart is null) return;
        EndRubberBandSelection();
        _viewModel.NotifyPreviewSelectionChanged();
        e.Handled = true;
    }

    private void RubberBandAutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (_rubberBandStart is not { } start || Mouse.LeftButton != MouseButtonState.Pressed)
        {
            EndRubberBandSelection();
            return;
        }

        var pointer = Mouse.GetPosition(PreviewPhotoScrollViewer);
        var delta = _edgeAutoScrollPolicy.Calculate(
            pointer,
            new System.Windows.Size(PreviewPhotoScrollViewer.ViewportWidth, PreviewPhotoScrollViewer.ViewportHeight));
        if (Math.Abs(delta.X) < 0.01 && Math.Abs(delta.Y) < 0.01) return;

        var horizontal = Math.Clamp(
            PreviewPhotoScrollViewer.HorizontalOffset + delta.X,
            0,
            PreviewPhotoScrollViewer.ScrollableWidth);
        var vertical = Math.Clamp(
            PreviewPhotoScrollViewer.VerticalOffset + delta.Y,
            0,
            PreviewPhotoScrollViewer.ScrollableHeight);
        PreviewPhotoScrollViewer.ScrollToHorizontalOffset(horizontal);
        PreviewPhotoScrollViewer.ScrollToVerticalOffset(vertical);
        PreviewPhotoScrollViewer.UpdateLayout();
        UpdateRubberBandSelection(start, Mouse.GetPosition(PreviewSelectionSurface));
    }

    private void EndRubberBandSelection()
    {
        _rubberBandAutoScrollTimer.Stop();
        _rubberBandStart = null;
        _rubberBandBaseline.Clear();
        PreviewSelectionRectangle.Visibility = Visibility.Collapsed;
        if (PreviewSelectionSurface.IsMouseCaptured) PreviewSelectionSurface.ReleaseMouseCapture();
    }

    private static FrameworkElement? FindPreviewCard(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement element && Equals(element.Tag, "PreviewCard")) return element;
            current = GetLogicalOrVisualParent(current);
        }
        return null;
    }

    private static T? FindVisualAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match) return match;
            current = GetLogicalOrVisualParent(current);
        }
        return null;
    }

    private static DependencyObject? GetLogicalOrVisualParent(DependencyObject current) =>
        current switch
        {
            System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(current),
            FrameworkContentElement content => content.Parent,
            _ => null
        };

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualDescendants<T>(child)) yield return descendant;
        }
    }

    private void PreviewThumbnail_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed
            && sender is System.Windows.FrameworkElement element
            && element.DataContext is PreviewFileViewModel file
            && System.IO.File.Exists(file.PreviewPath))
        {
            var data = new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new[] { file.PreviewPath });
            DragDrop.DoDragDrop(element, data, System.Windows.DragDropEffects.Copy);
        }
    }

    private void PreviewThumbnail_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // The ContextMenu is defined on the Border; its DataContext naturally
        // inherits from the Border's DataContext (which is PreviewFileViewModel).
        // Tag the menu so handlers can quickly retrieve the file path.
        if (sender is System.Windows.FrameworkElement element
            && element.DataContext is PreviewFileViewModel file)
        {
            element.ContextMenu.Tag = file;
        }
    }

    private void PreviewContextMenu_Open(object sender, RoutedEventArgs e)
    {
        var file = (sender as System.Windows.FrameworkElement)?.FindContextMenuTag() as PreviewFileViewModel;
        if (file is not null && System.IO.File.Exists(file.PreviewPath))
        {
            OpenIndependentViewer(file);
        }
    }

    private void OpenIndependentViewer(PreviewFileViewModel file)
    {
        var paths = _viewModel.VisiblePreviewFiles.Select(item => item.PreviewPath)
            .Where(path => System.IO.File.Exists(path)).ToArray();
        var window = new PhotoViewerWindow(paths, file.PreviewPath, _viewModel.RemoveDeletedViewerPhoto) { Owner = this };
        window.Show();
    }

    private void PreviewContextMenu_OpenFolder(object sender, RoutedEventArgs e)
    {
        var file = (sender as System.Windows.FrameworkElement)?.FindContextMenuTag() as PreviewFileViewModel;
        if (file is not null && System.IO.File.Exists(file.PreviewPath))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file.PreviewPath}\"");
        }
    }

    private void PreviewContextMenu_CopyPath(object sender, RoutedEventArgs e)
    {
        var file = (sender as System.Windows.FrameworkElement)?.FindContextMenuTag() as PreviewFileViewModel;
        if (file is not null)
        {
            System.Windows.Clipboard.SetText(file.PreviewPath);
            _viewModel.StatusMessage = $"已复制：{file.PreviewPath}";
        }
    }
}

file static class ContextMenuExtensions
{
    public static object? FindContextMenuTag(this System.Windows.FrameworkElement child)
    {
        var parent = child as System.Windows.Controls.MenuItem;
        while (parent is not null)
        {
            object? tag = null;
            if (parent.Parent is ContextMenu menu)
            {
                tag = menu.Tag;
            }
            if (tag is not null)
            {
                return tag;
            }
            parent = parent.Parent as System.Windows.Controls.MenuItem;
        }
        return null;
    }
}
