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
    private readonly string[] _idleMessages = ["Zzz…", "我在这里。", "照片整理好了吗？", "需要我时叫我～", "休息一下吧。"];
    private readonly Random _random = new();
    private MainWindowViewModel? _viewModel;

    public HanabeAssistantWindow()
    {
        InitializeComponent();
        _idleActionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(14) };
        _idleActionTimer.Tick += IdleActionTimer_Tick;
        Loaded += (_, _) => { AttachViewModel(); UpdatePresentation(); _idleActionTimer.Start(); };
        DataContextChanged += (_, _) => { AttachViewModel(); UpdatePresentation(); };
        SizeChanged += (_, _) => PositionAtWorkAreaEdge();
        Closed += (_, _) => { _idleActionTimer.Stop(); DetachViewModel(); };
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
        ActiveTaskPanel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        IdleSpeechBubble.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        ShellBorder.Background = active
            ? (System.Windows.Media.Brush)FindResource("Brush.Surface.Elevated")
            : System.Windows.Media.Brushes.Transparent;
        ShellBorder.Padding = active ? new Thickness(12) : new Thickness(0);
        Width = active ? 380 : 190;
        MinHeight = active ? 104 : 88;
        if (!active) PlayIdleMotion();
        PositionAtWorkAreaEdge();
    }

    private void IdleActionTimer_Tick(object? sender, EventArgs e)
    {
        if (ActiveTaskPanel.Visibility == Visibility.Visible) return;
        IdleSpeechText.Text = _idleMessages[_random.Next(_idleMessages.Length)];
        _idleActionTimer.Interval = TimeSpan.FromSeconds(_random.Next(11, 23));
        PlayIdleMotion();
    }

    private void PlayIdleMotion()
    {
        AvatarTranslation.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(2.4),
                RepeatBehavior = RepeatBehavior.Forever,
                KeyFrames =
                {
                    new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0)),
                    new EasingDoubleKeyFrame(-3, KeyTime.FromPercent(.5)),
                    new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1))
                }
            });
        AvatarRotation.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(-1.5, 1.5, TimeSpan.FromSeconds(3.2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
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

        if (e.OriginalSource is DependencyObject source && FindParent<System.Windows.Controls.Primitives.ButtonBase>(source) is not null)
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
