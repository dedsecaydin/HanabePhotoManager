using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
    private System.Windows.Point? _navigationDragStart;
    private NavigationItemViewModel? _navigationDragSource;
    private CancellationTokenSource? _cloudTransitionCts;

    public MainWindow()
    {
        _rubberBandAutoScrollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(24)
        };
        _rubberBandAutoScrollTimer.Tick += RubberBandAutoScrollTimer_Tick;
        _windowStateSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _windowStateSaveTimer.Tick += (_, _) => { _windowStateSaveTimer.Stop(); PersistWindowState(); };
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
        Activated += (_, _) => _viewModel.RefreshWindowsWallpaper();
        Deactivated += (_, _) => EndRubberBandSelection();
        LocationChanged += (_, _) => ScheduleWindowStateSave();
        StateChanged += (_, _) => ScheduleWindowStateSave();
        _viewModel.PropertyChanged += MainWindowViewModel_PropertyChanged;
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
        AnimateVisiblePage();
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        ThemeToggleButton.Content = ThemeManager.Current == AppTheme.Light ? "深色模式" : "浅色模式";
    }

    private void MainWindowViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentPage))
        {
            Dispatcher.BeginInvoke(AnimateVisiblePage);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.AppIconImage))
        {
            ApplyCustomWindowIcon();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.SelectedCloudProvider))
        {
            AnimateCloudProvider();
        }
    }

    private void ApplyCustomWindowIcon()
    {
        Icon = _viewModel.AppIconImage ?? DefaultAppIcon;
    }

    private void AnimateVisiblePage()
    {
        FrameworkElement page = _viewModel.CurrentPage switch
        {
            "Import" => ImportPage,
            "Preview" => PreviewPage,
            "FaceSearch" => FaceSearchPage,
            "MapPhotos" => MapPageHost,
            "Compression" => CompressionPageHost,
            "Cloud" => CloudPageContainer,
            "Settings" => SettingsPage,
            _ => HomePage
        };

        if (!SystemParameters.ClientAreaAnimation)
        {
            page.Opacity = 1;
            page.RenderTransform = new TranslateTransform();
            return;
        }

        page.Opacity = 0;
        page.RenderTransform = new TranslateTransform(0, 18);

        var storyboard = new Storyboard();
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fade, page);
        Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));

        var slide = new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slide, page);
        Storyboard.SetTargetProperty(slide, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

        storyboard.Children.Add(fade);
        storyboard.Children.Add(slide);
        storyboard.Begin();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cloudTransitionCts?.Cancel();
        _cloudTransitionCts?.Dispose();
        _windowStateSaveTimer.Stop();
        PersistWindowState();
        _viewModel.PropertyChanged -= MainWindowViewModel_PropertyChanged;
        _viewModel.FaceSearch.Cancel();
        MapPageHost.Dispose();
        BaiduCloudPageHost.Dispose();
        QuarkCloudPageHost.Dispose();
        base.OnClosed(e);
    }

    private async void AnimateCloudProvider()
    {
        _cloudTransitionCts?.Cancel();
        _cloudTransitionCts?.Dispose();
        var cancellation = _cloudTransitionCts = new CancellationTokenSource();

        var incoming = _viewModel.SelectedCloudProvider == CloudProviderChoice.Baidu
            ? BaiduCloudPageHost
            : QuarkCloudPageHost;
        var outgoing = ReferenceEquals(incoming, BaiduCloudPageHost)
            ? QuarkCloudPageHost
            : BaiduCloudPageHost;

        incoming.Visibility = Visibility.Visible;
        incoming.BeginAnimation(OpacityProperty, null);

        if (!SystemParameters.ClientAreaAnimation)
        {
            incoming.Opacity = 1;
            outgoing.Opacity = 0;
            outgoing.Visibility = Visibility.Collapsed;
            return;
        }

        incoming.Opacity = 0;
        incoming.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(180), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!ReferenceEquals(_cloudTransitionCts, cancellation)) return;
        incoming.BeginAnimation(OpacityProperty, null);
        incoming.Opacity = 1;
        outgoing.BeginAnimation(OpacityProperty, null);
        outgoing.Opacity = 0;
        outgoing.Visibility = Visibility.Collapsed;
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

    private void LibraryDates_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is LibraryDateNode node)
        {
            _viewModel.SelectedDate = node;
        }
    }

    private void PreviewScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _viewModel.AdjustThumbnailSize(e.Delta > 0);
            e.Handled = true;
        }
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

    private void BaiduAppSecretBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            _viewModel.BaiduAppSecret = passwordBox.Password;
        }
    }

    private void BrowseQuarkClient_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择夸克客户端或桌面快捷方式",
            Filter = "可执行文件 (*.exe;*.lnk;*.url)|*.exe;*.lnk;*.url|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.QuarkClientPath = dialog.FileName;
        }
    }

    private void LaunchQuarkClient_Click(object sender, RoutedEventArgs e)
    {
        var path = _viewModel.QuarkClientPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            _viewModel.StatusMessage = "请先在左侧填入或选择夸克客户端路径。";
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            _viewModel.StatusMessage = $"已启动：{path}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"启动夸克失败：{ex.Message}";
        }
    }

    private void CloseExifPanel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _viewModel.SelectedPreviewFile = null;
    }

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

    private void PreviewContextMenu_BatchCopy(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择目标文件夹" };
        if (dlg.ShowDialog() != true) return;
        _viewModel.BatchCopyFilesTo(dlg.FolderName);
    }

    private void PreviewContextMenu_BatchMove(object sender, RoutedEventArgs e)
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
        var file = _viewModel.SelectedPreviewFile;
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
        {
            return;
        }

        if (_viewModel.PhotoViewer.IsOpen)
        {
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
    }

    private void PreviewThumbnail_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement element
            && element.DataContext is PreviewFileViewModel file)
        {
            if (FindVisualAncestor<System.Windows.Controls.CheckBox>(e.OriginalSource as DependencyObject) is not null)
            {
                return;
            }

            _viewModel.SelectedPreviewFile = file;
            var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            if (!control && !shift && e.ClickCount == 1)
            {
                OpenIndependentViewer(file);
                e.Handled = true;
                return;
            }

            if (shift)
            {
                SelectPreviewRange(file, additive: control);
            }
            else if (control)
            {
                file.IsSelected = !file.IsSelected;
                _selectionAnchor = file;
            }
            else
            {
                foreach (var item in _viewModel.PreviewFiles) item.IsSelected = ReferenceEquals(item, file);
                _selectionAnchor = file;
            }

            _viewModel.NotifyPreviewSelectionChanged();

            // Double click: open file
            if (e.ClickCount == 2 && System.IO.File.Exists(file.PreviewPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file.PreviewPath) { UseShellExecute = true });
            }
        }
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
        foreach (var card in FindVisualDescendants<FrameworkElement>(PreviewSelectionSurface)
                     .Where(element => Equals(element.Tag, "PreviewCard")))
        {
            if (card.DataContext is not PreviewFileViewModel item) continue;
            var bounds = card.TransformToAncestor(PreviewSelectionSurface)
                .TransformBounds(new Rect(new System.Windows.Point(0, 0), card.RenderSize));
            var hit = selection.IntersectsWith(bounds);
            var baseline = _rubberBandBaseline.GetValueOrDefault(item);
            item.IsSelected = toggle ? hit != baseline : hit;
        }

        _viewModel.NotifyPreviewSelectionChanged();
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
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindVisualAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

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
