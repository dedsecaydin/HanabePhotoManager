using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.ComponentModel;
using System.Windows.Threading;
using HanabePhotoManager.App.ViewModels;

namespace HanabePhotoManager.App;

public partial class HanabeAssistantWindow : Window
{
    private readonly DispatcherTimer _idleActionTimer;
    private readonly DispatcherTimer _singleClickTimer;
    private readonly string[] _idleMessages = ["Zzz…", "我在这里。", "照片整理好了吗？", "需要我时叫我～", "休息一下吧。"];
    private readonly Random _random = new();
    private MainWindowViewModel? _viewModel;
    private int _idleActionIndex;
    private System.Windows.Point _avatarPointerStart;
    private bool _avatarDragCandidate;

    public HanabeAssistantWindow()
    {
        InitializeComponent();
        _idleActionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(14) };
        _idleActionTimer.Tick += IdleActionTimer_Tick;
        _singleClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _singleClickTimer.Tick += (_, _) =>
        {
            _singleClickTimer.Stop();
            if (ActiveTaskPanel.Visibility != Visibility.Visible) PlayNextIdleAction();
        };
        Loaded += (_, _) => { AttachViewModel(); UpdatePresentation(); _idleActionTimer.Start(); };
        DataContextChanged += (_, _) => { AttachViewModel(); UpdatePresentation(); };
        SizeChanged += (_, _) => PositionAtWorkAreaEdge();
        Closed += (_, _) => { _idleActionTimer.Stop(); _singleClickTimer.Stop(); DetachViewModel(); };
        PositionAtWorkAreaEdge();
    }

    private void AttachViewModel()
    {
        DetachViewModel();
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is null) return;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.Recovery.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.Compression.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void DetachViewModel()
    {
        if (_viewModel is null) return;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.Recovery.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.Compression.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = null;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) => Dispatcher.BeginInvoke(UpdatePresentation);

    private void UpdatePresentation()
    {
        if (_viewModel is null) return;
        var active = _viewModel.AssistantSnapshot.IsProgressVisible || _viewModel.Recovery.IsBusy ||
                     _viewModel.Compression.IsRunning || _viewModel.Compression.IsScanning;
        var avatarWidth = _viewModel.AssistantAvatarSize;
        var avatarHeight = avatarWidth * 7 / 6;
        AssistantAvatar.Width = avatarWidth;
        AssistantAvatar.Height = avatarHeight;
        AvatarColumn.Width = new GridLength(avatarWidth + 4);
        ActiveTaskPanel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        IdleSpeechBubble.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        ShellBorder.Background = active
            ? (System.Windows.Media.Brush)FindResource("Brush.Surface.Elevated")
            : System.Windows.Media.Brushes.Transparent;
        ShellBorder.Padding = active ? new Thickness(12) : new Thickness(0);
        Width = active ? Math.Max(420, avatarWidth + 324) : avatarWidth + 154;
        MinHeight = active ? Math.Max(136, avatarHeight + 24) : avatarHeight + 16;
        if (active) ResetAvatarMotion(); else PlayIdleAction(_idleActionIndex);
        PositionAtWorkAreaEdge();
    }

    private void IdleActionTimer_Tick(object? sender, EventArgs e)
    {
        if (ActiveTaskPanel.Visibility == Visibility.Visible) return;
        _idleActionTimer.Interval = TimeSpan.FromSeconds(_random.Next(11, 23));
        _idleActionIndex = _random.Next(4);
        PlayIdleAction(_idleActionIndex);
    }

    private void PlayNextIdleAction()
    {
        _idleActionIndex = (_idleActionIndex + 1) % 4;
        PlayIdleAction(_idleActionIndex);
    }

    private void PlayIdleAction(int action)
    {
        ResetAvatarMotion();
        IdleSpeechText.Text = action switch
        {
            1 => "蹦一下～",
            2 => "嗯？",
            3 => "Zzz…",
            _ => _idleMessages[_random.Next(_idleMessages.Length)]
        };

        if (action == 1)
        {
            AvatarTranslation.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(.85),
                RepeatBehavior = RepeatBehavior.Forever,
                KeyFrames =
                {
                    new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0)),
                    new EasingDoubleKeyFrame(-12, KeyTime.FromPercent(.38)),
                    new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1))
                }
            });
            return;
        }

        if (action == 2)
        {
            AvatarRotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-5, 5, TimeSpan.FromSeconds(1.15)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            return;
        }

        if (action == 3)
        {
            AvatarRotation.Angle = 4;
            AvatarTranslation.Y = 4;
            AvatarScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(.94, 1, TimeSpan.FromSeconds(1.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            return;
        }

        AvatarTranslation.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-3, 1, TimeSpan.FromSeconds(1.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        AvatarRotation.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(-1.5, 1.5, TimeSpan.FromSeconds(3.2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }

    private void ResetAvatarMotion()
    {
        AvatarTranslation.BeginAnimation(TranslateTransform.YProperty, null);
        AvatarRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        AvatarScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        AvatarTranslation.Y = 0;
        AvatarRotation.Angle = 0;
        AvatarScale.ScaleX = 1;
        AvatarScale.ScaleY = 1;
    }

    private void AssistantAvatar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ClickCount >= 2)
        {
            _singleClickTimer.Stop();
            _avatarDragCandidate = false;
            RestoreMainWindow();
            return;
        }

        _avatarPointerStart = e.GetPosition(this);
        _avatarDragCandidate = true;
        AssistantAvatar.CaptureMouse();
    }

    private void AssistantAvatar_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_avatarDragCandidate || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _avatarPointerStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _avatarPointerStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _avatarDragCandidate = false;
        AssistantAvatar.ReleaseMouseCapture();
        try { DragMove(); }
        catch (InvalidOperationException) { }
        e.Handled = true;
    }

    private void AssistantAvatar_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        AssistantAvatar.ReleaseMouseCapture();
        if (!_avatarDragCandidate) return;
        _avatarDragCandidate = false;
        _singleClickTimer.Stop();
        _singleClickTimer.Start();
        e.Handled = true;
    }

    private void PositionAtWorkAreaEdge()
    {
        var area = SystemParameters.WorkArea;
        Left = Math.Max(area.Left, area.Right - Width - 24);
        Top = Math.Max(area.Top, area.Bottom - ActualHeight - 24);
    }

    private void Surface_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source &&
            (FindParent<System.Windows.Controls.Primitives.ButtonBase>(source) is not null ||
             ReferenceEquals(FindParent<System.Windows.Controls.Border>(source), AssistantAvatar)))
        {
            return;
        }

        try { DragMove(); }
        catch (InvalidOperationException) { }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void OpenHanabe_Click(object sender, RoutedEventArgs e) => RestoreMainWindow();

    private async void Resume_Click(object sender, RoutedEventArgs e)
    {
        RestoreMainWindow();
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ResumePendingImportAsync();
        }
    }

    private static void RestoreMainWindow()
    {
        if (System.Windows.Application.Current.MainWindow is not { } main) return;
        main.Show();
        main.WindowState = WindowState.Normal;
        main.Activate();
    }

    private static T? FindParent<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match) return match;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        return null;
    }
}
