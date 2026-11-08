using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Colors = System.Windows.Media.Colors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Path = System.Windows.Shapes.Path;
using Point = System.Windows.Point;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace HanabePhotoManager.App;

/// <summary>
/// Hanabe 桌宠 v2「显影」：主窗口最小化后住在任务栏上的摄影师。
/// 没有任务时栖息；后台任务进行时手里的拍立得随进度显影；悬停把拍立得举起翻到背面看详情；
/// 拖到屏幕右边（或点背面的 ×）会躲成屏幕边缘的探头。
/// </summary>
public partial class HanabeAssistantWindow : Window
{
    private const double SurfaceWidth = 620, SurfaceHeight = 540, PeekWidth = 130, PeekHeight = 170;
    private static readonly Point HeadPt = new(SurfaceWidth - 130, SurfaceHeight - 84);
    private static readonly Point CameraPt = new(SurfaceWidth - 158, SurfaceHeight - 34);
    private static readonly Point PolPt = new(SurfaceWidth - 280, SurfaceHeight - 100);
    private static readonly Point PilePt = new(SurfaceWidth - 418, SurfaceHeight - 30);
    private static readonly Geometry StarGeometry = Geometry.Parse("M0,-1 C0.1,-0.1 0.1,-0.1 1,0 C0.1,0.1 0.1,0.1 0,1 C-0.1,0.1 -0.1,0.1 -1,0 C-0.1,-0.1 -0.1,-0.1 0,-1 Z");

    private sealed record PetLook(string Caption, string Bubble, string Tone, string PeekLabel);
    private sealed record PetOutfit(string File, Color Accent, Color Paper, string[] Lines);

    private static readonly Dictionary<string, Color> Tones = new()
    {
        ["active"] = Hex("#FF7AA8"), ["neutral"] = Hex("#C9B8D6"), ["recover"] = Hex("#8FB8FF"),
        ["ok"] = Hex("#7FE0B8"), ["warn"] = Hex("#FFB866"), ["bad"] = Hex("#FF7A8A")
    };

    private static readonly Dictionary<string, Color> Inks = new()
    {
        ["active"] = Hex("#D62B66"), ["neutral"] = Hex("#7A6680"), ["recover"] = Hex("#3F6FC4"),
        ["ok"] = Hex("#2E8F6A"), ["warn"] = Hex("#B9741A"), ["bad"] = Hex("#C8233F")
    };

    private static readonly Dictionary<string, PetLook> Looks = new()
    {
        ["Idle"] = new("Standby", "今天也拍到喜欢的画面了吗？", "neutral", ""),
        ["Scanning"] = new("Looking…", "我看看卡里都有什么～", "active", "扫描"),
        ["Checking"] = new("Compare", "这两张…是同一张吗？", "active", "校验"),
        ["Importing"] = new("Developing", "正在显影～", "active", ""),
        ["StopRequested"] = new("One sec", "稍等，这张收好就停。", "neutral", "停止中"),
        ["Canceled"] = new("Paused here", "剩下的下次再说～", "neutral", "已停止"),
        ["Interrupted"] = new("Oops", "诶？好像被打断了…", "warn", "!"),
        ["Recoverable"] = new("To be continued", "上次的还没传完哦。", "recover", "待继续"),
        ["Resuming"] = new("Rewind", "从上次停下的地方开始～", "active", "恢复"),
        ["Completed"] = new("All done!", "咔嚓！全部收好啦～", "ok", "完成"),
        ["CompletedWithIssues"] = new("Almost", "大部分收好了，有几张不太对…", "warn", "需处理"),
        ["Failed"] = new("Oh no", "出了点问题…", "bad", "失败"),
        ["Error"] = new("Oh no", "出了点问题…", "bad", "出错"),
        ["Busy"] = new("Working", "我在后台帮你处理～", "active", "")
    };

    private static readonly PetOutfit Street = new("street.png", Hex("#FF5A7A"), Hex("#F7F1EC"), ["今天也拍到喜欢的画面了吗？", "咔嚓～刚才那一下很好看哦。", "SD 卡插进来我就开工！"]);
    private static readonly PetOutfit Idol = new("idol.png", Hex("#FF6FC0"), Hex("#FFF0F6"), ["舞台返图交给我！", "啦啦～今天也要元气满满。"]);
    private static readonly PetOutfit Rainy = new("rainy.png", Hex("#7FA8F0"), Hex("#EEF4FF"), ["下雨天，适合整理照片。", "雨声好安静呀。"]);
    private static readonly PetOutfit Holy = new("holy.png", Hex("#D9B263"), Hex("#FBF5E6"), ["每一张都值得被好好保存。", "翻翻以前的相册吧。"]);
    private static readonly PetOutfit Encore = new("encore.png", Hex("#E0405E"), Hex("#F5ECEC"), ["安可！再导入一卡吧～", "试音、试音～"]);

    private readonly Random _random = new();
    private readonly List<UIElement> _stateFx = [];
    private readonly DispatcherTimer _hoverIn = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _hoverOut = new() { Interval = TimeSpan.FromMilliseconds(1400) };
    private readonly DispatcherTimer _ambient = new() { Interval = TimeSpan.FromMilliseconds(140) };
    private readonly DispatcherTimer _chatter = new() { Interval = TimeSpan.FromSeconds(40) };
    private readonly DispatcherTimer _bubbleHide = new();
    private readonly DispatcherTimer _peekNudge = new() { Interval = TimeSpan.FromSeconds(7) };
    private MainWindowViewModel? _viewModel;
    private PetOutfit _outfit = Street;
    private HanabeAssistantVisualStyle? _appliedStyle;
    private string _stateKey = "";
    private string _tone = "neutral";
    private bool _persistent;
    private bool _calm;
    private bool _raised;
    private bool _peek;
    private bool _hasUserPosition;
    private double _scale = 1;
    private double _savedLeft, _savedTop;
    private int _lastSuccess;
    private DateTime _lastEject = DateTime.MinValue;
    private DateTime _lastInteract = DateTime.Now;

    public HanabeAssistantWindow()
    {
        InitializeComponent();
        _hoverIn.Tick += (_, _) => { _hoverIn.Stop(); OnHover(); };
        _hoverOut.Tick += (_, _) => { _hoverOut.Stop(); Lower(); };
        _chatter.Tick += (_, _) => { if (_stateKey == "Idle" && !_peek && !_calm) Say(_outfit.Lines[_random.Next(_outfit.Lines.Length)], null, 6); };
        _bubbleHide.Tick += (_, _) => { _bubbleHide.Stop(); HideBubble(); };
        _ambient.Tick += AmbientTick;
        _peekNudge.Tick += (_, _) => PeekNudge();
        MouseEnter += (_, _) => { if (_peek) return; _hoverOut.Stop(); if (!_raised) _hoverIn.Start(); };
        MouseLeave += (_, _) => { _hoverIn.Stop(); if (_raised && !_persistent) _hoverOut.Start(); };
        Loaded += (_, _) => { AttachViewModel(); UpdatePresentation(); StartLoops(); };
        DataContextChanged += (_, _) => { AttachViewModel(); UpdatePresentation(); };
        IsVisibleChanged += (_, _) => { if (IsVisible) { _ambient.Start(); _chatter.Start(); UpdatePresentation(); } else { _ambient.Stop(); _chatter.Stop(); } };
        Closed += (_, _) => { _ambient.Stop(); _chatter.Stop(); _peekNudge.Stop(); DetachViewModel(); };
        PositionAutomatically();
    }

    // ===================== view model =====================
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
        var vm = _viewModel;
        if (vm is null) return;
        ApplyStyleAndSize(vm);

        var snapshot = vm.AssistantSnapshot;
        var recoveryBusy = vm.Recovery.IsBusy;
        var compressionBusy = vm.Compression.IsRunning || vm.Compression.IsScanning;
        var key = vm.AssistantState.ToString();
        string chip, title, detail, hint;
        bool showBar, indeterminate;
        double progress;
        if (vm.AssistantState == HanabeAssistantState.Idle && (recoveryBusy || compressionBusy))
        {
            key = "Busy";
            chip = recoveryBusy ? "恢复扫描" : "图片处理";
            title = chip;
            detail = recoveryBusy ? vm.Recovery.StatusText : vm.Compression.StatusText;
            hint = "完成后我会告诉你～";
            progress = recoveryBusy ? vm.Recovery.ProgressValue : vm.Compression.ProgressValue;
            showBar = true;
            indeterminate = false;
        }
        else
        {
            chip = snapshot.StatusLabel;
            title = snapshot.TaskTitle;
            detail = snapshot.Detail;
            hint = snapshot.ResultHint;
            progress = snapshot.ProgressValue;
            showBar = snapshot.IsProgressVisible;
            indeterminate = snapshot.IsProgressIndeterminate;
        }

        var look = Looks.TryGetValue(key, out var found) ? found : Looks["Idle"];
        var changed = key != _stateKey;
        _stateKey = key;
        _tone = look.Tone;
        _persistent = snapshot.IsPersistent && key != "Busy";
        var color = Tones[look.Tone];
        var ink = Inks[look.Tone];
        var determinate = showBar && !indeterminate;
        var success = vm.ImportSuccessCount;
        var eta = vm.EstimatedTimeRemaining ?? string.Empty;

        PolDot.Fill = new SolidColorBrush(color);
        PolChip.Text = chip;
        PolCaption.Text = look.Caption;
        PolCount.Text = determinate ? $"{progress:0}%" : success > 0 ? $"{success:N0} 张" : string.Empty;
        BackDot.Fill = new SolidColorBrush(ink);
        BackChip.Foreground = new SolidColorBrush(ink);
        BackChip.Text = chip;
        BackTitle.Text = title;
        BackDetail.Text = detail;
        BackDetail.Visibility = string.IsNullOrWhiteSpace(detail) ? Visibility.Collapsed : Visibility.Visible;
        BackHint.Text = hint;
        BackBar.Visibility = showBar ? Visibility.Visible : Visibility.Collapsed;
        BackBarFill.Background = new SolidColorBrush(ink);
        BackPct.Foreground = new SolidColorBrush(ink);
        BackPct.Text = determinate ? $"{progress:0}%" : indeterminate ? "总量确认中" : string.Empty;
        BackEta.Text = showBar ? eta : string.Empty;
        FilmFill.Fill = new SolidColorBrush(color);
        FilmFill.Visibility = determinate || key == "Completed" ? Visibility.Visible : Visibility.Collapsed;
        FilmShimmer.Visibility = showBar && indeterminate ? Visibility.Visible : Visibility.Collapsed;
        var total = vm.ImportSuccessCount + vm.ImportSkippedCount + vm.ImportFailedCount;
        BackStats.Visibility = total > 0 && key != "Busy" ? Visibility.Visible : Visibility.Collapsed;
        StatOk.Text = vm.ImportSuccessCount.ToString("N0");
        StatSkip.Text = vm.ImportSkippedCount.ToString("N0");
        StatFail.Text = vm.ImportFailedCount.ToString("N0");
        PeekDot.Fill = new SolidColorBrush(color);
        PeekText.Foreground = new SolidColorBrush(color);
        PeekText.Text = determinate && look.Tone == "active" ? $"{progress:0}%" : look.PeekLabel;

        if (determinate) SetDevelop(progress / 100.0);
        else if (key == "Completed") SetDevelop(1);
        else if (indeterminate) SetDevelop(0.1);

        // 进行中的状态：她一直在说进度
        var bubbleText = key switch
        {
            "Importing" or "Resuming" when success > 0 => $"已经收好 {success:N0} 张～",
            _ => look.Bubble
        };
        var bubbleSub = showBar ? string.Join(" · ", new[] { determinate ? $"{progress:0}%" : string.Empty, eta }.Where(s => !string.IsNullOrWhiteSpace(s))) : null;

        if (key is "Importing" or "Resuming" && success > _lastSuccess && !_calm && !_peek &&
            (DateTime.Now - _lastEject).TotalMilliseconds > 900)
        {
            _lastEject = DateTime.Now;
            EjectToPile(success);
        }
        else if (PileBadge.Opacity > 0.5 && success > 0)
        {
            PileText.Text = $"已收 {success:N0}";
        }
        _lastSuccess = success;

        if (!changed)
        {
            if (look.Tone == "active" && !_raised && !_peek && Bubble.Opacity > 0.5) SetBubbleText(bubbleText, bubbleSub);
            return;
        }

        RenderActions(snapshot, key);
        ClearStateFx();
        Halo.BeginAnimation(OpacityProperty, new DoubleAnimation(key == "Idle" ? 0 : 0.9, TimeSpan.FromMilliseconds(500)));
        HaloColor.Color = Color.FromArgb(0x66, color.R, color.G, color.B);
        Stamp.BeginAnimation(OpacityProperty, null);
        Stamp.Opacity = 0;
        Bookmark.Opacity = key is "Recoverable" or "Resuming" or "Interrupted" or "Canceled" ? 1 : 0;

        var develop = key != "Idle";
        if (develop && Polaroid.Visibility != Visibility.Visible) ShowPolaroid(true);
        if (!develop && Polaroid.Visibility == Visibility.Visible) { Lower(); ShowPolaroid(false); }
        if (!develop || key == "Scanning") ClearPile();

        if (!_peek)
        {
            if (key == "Idle") HideBubble();
            else Say(bubbleText, bubbleSub, look.Tone == "active" ? 0 : 6);
            StartStateFx(key);
        }
        if (key == "Completed") Celebrate();
        if (_persistent)
        {
            if (_peek && look.Tone is "warn" or "bad") RestoreFromPeek();
            After(0.9, () => { if (_stateKey == key && !_peek) Raise(); });
        }
        if (_peek) RefreshPeekVisibility();
    }

    private void ApplyStyleAndSize(MainWindowViewModel vm)
    {
        var scale = Math.Clamp(vm.AssistantAvatarSize / 96.0, 0.7, 1.5);
        if (Math.Abs(scale - _scale) > 0.001)
        {
            _scale = scale;
            RootScale.ScaleX = RootScale.ScaleY = scale;
            ApplyWindowSize();
        }

        var style = vm.AssistantVisualStyle;
        if (_appliedStyle == style) return;
        _appliedStyle = style;
        _calm = style == HanabeAssistantVisualStyle.Static || !SystemParameters.ClientAreaAnimation;
        _outfit = style switch
        {
            HanabeAssistantVisualStyle.IdolAnimated => Idol,
            HanabeAssistantVisualStyle.RainyAnimated or HanabeAssistantVisualStyle.WinterAnimated => Rainy,
            HanabeAssistantVisualStyle.OldMoneyAnimated => Holy,
            HanabeAssistantVisualStyle.EncoreAnimated => Encore,
            _ => Street
        };
        var pixel = style == HanabeAssistantVisualStyle.PixelAnimated;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri($"pack://application:,,,/Assets/Hanabe/Outfits/{_outfit.File}", UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (pixel) bitmap.DecodePixelWidth = 104;
        bitmap.EndInit();
        bitmap.Freeze();
        Bust.Source = bitmap;
        RenderOptions.SetBitmapScalingMode(Bust, pixel ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
        PeekImg.Source = new CroppedBitmap(bitmap, new Int32Rect((int)(bitmap.PixelWidth * 0.2), 0, (int)(bitmap.PixelWidth * 0.6), (int)(bitmap.PixelHeight * 0.58)));
        RenderOptions.SetBitmapScalingMode(PeekImg, pixel ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
        PolFront.Background = PolBack.Background = new SolidColorBrush(_outfit.Paper);
        PeekGlowColor.Color = Color.FromArgb(0x99, _outfit.Accent.R, _outfit.Accent.G, _outfit.Accent.B);
        StartLoops();
    }

    private void RenderActions(HanabeAssistantSnapshot snapshot, string key)
    {
        BackActions.Children.Clear();
        var actions = new List<(string Label, bool Primary, Action Run)>();
        if (snapshot.CanStop) actions.Add(("停止", false, () => _viewModel?.CancelCurrentTaskCommand.Execute(null)));
        if (snapshot.CanViewReport) actions.Add(("查看", !snapshot.CanResume, RestoreMainWindow));
        if (snapshot.CanResume) actions.Add(("继续", true, ResumeImport));
        if (actions.Count == 0 && key != "Idle") actions.Add(("打开", true, RestoreMainWindow));
        var tilt = 0;
        foreach (var (label, primary, run) in actions)
        {
            var accent = _outfit.Accent;
            var button = new Button
            {
                Content = label,
                Style = (Style)FindResource("Pet.Sticker"),
                Background = primary ? new SolidColorBrush(Color.FromArgb(0xE6, accent.R, accent.G, accent.B)) : new SolidColorBrush(Color.FromArgb(0x55, 0xC9, 0xB8, 0xD6)),
                Foreground = primary ? Brushes.White : new SolidColorBrush(Hex("#3A2433")),
                RenderTransform = new RotateTransform(tilt++ % 2 == 0 ? -3 : 2.5),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            AutomationPropertiesHelper(button, label);
            button.Click += (_, _) => { _lastInteract = DateTime.Now; run(); };
            BackActions.Children.Add(button);
        }
    }

    private static void AutomationPropertiesHelper(Button button, string label) => System.Windows.Automation.AutomationProperties.SetName(button, label);

    private async void ResumeImport()
    {
        Lower();
        if (_viewModel is { } vm) await vm.ResumePendingImportAsync();
    }

    private void SetDevelop(double value)
    {
        var p = Math.Clamp(value, 0, 1);
        var duration = new Duration(TimeSpan.FromMilliseconds(_calm ? 1 : 600));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        DevDark.BeginAnimation(OpacityProperty, new DoubleAnimation(0.8 * (1 - p), duration) { EasingFunction = ease });
        SceneBlur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(5 * (1 - p), duration) { EasingFunction = ease });
        FilmFill.BeginAnimation(WidthProperty, new DoubleAnimation(150 * p, duration) { EasingFunction = ease });
        BackBarFill.BeginAnimation(WidthProperty, new DoubleAnimation(146 * p, duration) { EasingFunction = ease });
    }

    // ===================== polaroid =====================
    private void OnHover()
    {
        _lastInteract = DateTime.Now;
        if (_stateKey == "Idle")
        {
            Hop();
            Say("要看看我拍的照片吗？", "双击打开 Hanabe", 3.5);
            if (!_calm) Hearts(2);
            return;
        }
        Raise();
    }

    private void Polaroid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _lastInteract = DateTime.Now;
        if (_raised) Lower(); else Raise();
        e.Handled = true;
    }

    private void Raise()
    {
        if (_raised || _peek || Polaroid.Visibility != Visibility.Visible) return;
        _raised = true;
        HideBubble();
        var duration = TimeSpan.FromMilliseconds(380);
        var ease = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut };
        Animate(PolRaise, ScaleTransform.ScaleXProperty, 1.55, duration, ease);
        Animate(PolRaise, ScaleTransform.ScaleYProperty, 1.55, duration, ease);
        Animate(PolMove, TranslateTransform.YProperty, -46, duration, ease);
        Animate(PolMove, TranslateTransform.XProperty, 18, duration, ease);
        Animate(PolTilt, RotateTransform.AngleProperty, -1.5, duration, ease);
        Flip(true);
        if (!_calm) Sparkle(PolPt.X + 60, PolPt.Y - 80, Colors.White, 10);
    }

    private void Lower()
    {
        if (!_raised) return;
        _raised = false;
        var duration = TimeSpan.FromMilliseconds(320);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        Animate(PolRaise, ScaleTransform.ScaleXProperty, 1, duration, ease);
        Animate(PolRaise, ScaleTransform.ScaleYProperty, 1, duration, ease);
        Animate(PolMove, TranslateTransform.YProperty, 0, duration, ease);
        Animate(PolMove, TranslateTransform.XProperty, 0, duration, ease);
        Animate(PolTilt, RotateTransform.AngleProperty, -5, duration, ease);
        Flip(false);
        if (_tone == "active") After(0.35, () => { if (!_raised && Looks.TryGetValue(_stateKey, out var look)) Say(BubbleText.Text.Length > 0 ? BubbleText.Text : look.Bubble, null, 0); });
    }

    private void Flip(bool toBack)
    {
        var shrink = new DoubleAnimation(0, TimeSpan.FromMilliseconds(130)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn } };
        shrink.Completed += (_, _) =>
        {
            PolBack.Visibility = toBack ? Visibility.Visible : Visibility.Collapsed;
            PolFront.Visibility = toBack ? Visibility.Hidden : Visibility.Visible;
            PolFlip.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(170)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
        };
        PolFlip.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
    }

    private void ShowPolaroid(bool show)
    {
        if (show)
        {
            Polaroid.Visibility = Visibility.Visible;
            PolFront.Visibility = Visibility.Visible;
            PolBack.Visibility = Visibility.Collapsed;
            Polaroid.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));
            Animate(PolMove, TranslateTransform.YProperty, 0, TimeSpan.FromMilliseconds(520), new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }, 70);
            Animate(PolMove, TranslateTransform.XProperty, 0, TimeSpan.FromMilliseconds(520), new CubicEase { EasingMode = EasingMode.EaseOut }, 60);
            if (!_calm) for (var i = 0; i < 6; i++) Sparkle(CameraPt.X - _random.Next(0, 50), CameraPt.Y - _random.Next(0, 50), Colors.White, 6 + _random.Next(6));
            return;
        }
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260));
        fade.Completed += (_, _) => { if (_stateKey == "Idle") Polaroid.Visibility = Visibility.Collapsed; };
        Polaroid.BeginAnimation(OpacityProperty, fade);
        Animate(PolMove, TranslateTransform.YProperty, 50, TimeSpan.FromMilliseconds(260), null);
    }

    // ===================== bubble =====================
    private void Say(string text, string? sub, double seconds)
    {
        if (string.IsNullOrEmpty(text) || _peek || _raised) return;
        SetBubbleText(text, sub);
        if (Bubble.Opacity < 0.5)
        {
            Bubble.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
            var ease = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut };
            Animate(BubbleScale, ScaleTransform.ScaleXProperty, 1, TimeSpan.FromMilliseconds(320), ease, 0.6);
            Animate(BubbleScale, ScaleTransform.ScaleYProperty, 1, TimeSpan.FromMilliseconds(320), ease, 0.6);
        }
        _bubbleHide.Stop();
        if (seconds > 0)
        {
            _bubbleHide.Interval = TimeSpan.FromSeconds(seconds);
            _bubbleHide.Start();
        }
    }

    private void SetBubbleText(string text, string? sub)
    {
        BubbleText.Text = text;
        BubbleSub.Text = sub ?? string.Empty;
        BubbleSub.Visibility = string.IsNullOrEmpty(sub) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void HideBubble()
    {
        _bubbleHide.Stop();
        Bubble.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(160)));
    }

    // ===================== particles =====================
    private void Sparkle(double x, double y, Color color, double size, double rise = 22, double life = 0.9)
    {
        var star = new Path
        {
            Data = StarGeometry, Fill = new SolidColorBrush(color), Width = size * 2, Height = size * 2, Stretch = Stretch.Fill,
            RenderTransformOrigin = new Point(0.5, 0.5), Opacity = 0,
            Effect = new DropShadowEffect { Color = color, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 }
        };
        var scale = new ScaleTransform(0.2, 0.2);
        var rotate = new RotateTransform(_random.Next(0, 45));
        var move = new TranslateTransform();
        star.RenderTransform = new TransformGroup { Children = { scale, rotate, move } };
        Canvas.SetLeft(star, x - size);
        Canvas.SetTop(star, y - size);
        Fx.Children.Add(star);
        var duration = TimeSpan.FromSeconds(life);
        var opacity = new DoubleAnimationUsingKeyFrames { Duration = duration };
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.25)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        opacity.Completed += (_, _) => Fx.Children.Remove(star);
        var grow = new DoubleAnimationUsingKeyFrames { Duration = duration };
        grow.KeyFrames.Add(new EasingDoubleKeyFrame(1.1, KeyTime.FromPercent(0.3), new BackEase { EasingMode = EasingMode.EaseOut }));
        grow.KeyFrames.Add(new LinearDoubleKeyFrame(0.3, KeyTime.FromPercent(1)));
        star.BeginAnimation(OpacityProperty, opacity);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(rotate.Angle + 90, duration));
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-rise, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void Glyph(string text, double x, double y, Color color, double size, double rise, double life, double drift = 0)
    {
        var glyph = new TextBlock
        {
            Text = text, FontSize = size, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(color), Opacity = 0,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 1, Opacity = 0.5 }
        };
        var move = new TranslateTransform();
        glyph.RenderTransform = move;
        Canvas.SetLeft(glyph, x);
        Canvas.SetTop(glyph, y);
        Fx.Children.Add(glyph);
        var duration = TimeSpan.FromSeconds(life);
        var opacity = new DoubleAnimationUsingKeyFrames { Duration = duration };
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.2)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.6)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        opacity.Completed += (_, _) => Fx.Children.Remove(glyph);
        glyph.BeginAnimation(OpacityProperty, opacity);
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-rise, duration) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
        if (drift != 0) move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(drift, duration));
    }

    private void Puff(double x, double y)
    {
        var puff = new Ellipse { Width = 14, Height = 14, Fill = new SolidColorBrush(Color.FromArgb(0x99, 0xB8, 0xB0, 0xB8)), Opacity = 0, Effect = new BlurEffect { Radius = 4 }, RenderTransformOrigin = new Point(0.5, 0.5) };
        var scale = new ScaleTransform(0.5, 0.5);
        var move = new TranslateTransform();
        puff.RenderTransform = new TransformGroup { Children = { scale, move } };
        Canvas.SetLeft(puff, x - 7);
        Canvas.SetTop(puff, y - 7);
        Fx.Children.Add(puff);
        var duration = TimeSpan.FromSeconds(1.6);
        var opacity = new DoubleAnimationUsingKeyFrames { Duration = duration };
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(0.2)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        opacity.Completed += (_, _) => Fx.Children.Remove(puff);
        puff.BeginAnimation(OpacityProperty, opacity);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.8, duration));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.8, duration));
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-40, duration));
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(_random.Next(-14, 14), duration));
    }

    private Border MiniPolaroid(double scale = 1) => new()
    {
        Width = 22 * scale,
        Height = 26 * scale,
        Background = new SolidColorBrush(_outfit.Paper),
        CornerRadius = new CornerRadius(1.5),
        Child = new Border { Margin = new Thickness(2.5, 2.5, 2.5, 6), Background = new LinearGradientBrush(Hex("#5B3A6E"), Hex("#E58BA8"), 90) },
        RenderTransformOrigin = new Point(0.5, 0.5),
        Effect = new DropShadowEffect { BlurRadius = 5, ShadowDepth = 1.5, Opacity = 0.4 }
    };

    private void EjectToPile(int imported)
    {
        var mini = MiniPolaroid();
        var rotate = new RotateTransform(_random.Next(-30, 30));
        var moveX = new TranslateTransform();
        var moveY = new TranslateTransform();
        mini.RenderTransform = new TransformGroup { Children = { rotate, moveX, moveY } };
        Canvas.SetLeft(mini, CameraPt.X - 11);
        Canvas.SetTop(mini, CameraPt.Y - 30);
        Fx.Children.Add(mini);
        var duration = TimeSpan.FromMilliseconds(820);
        moveX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(PilePt.X - CameraPt.X + _random.Next(-14, 14), duration) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        var arc = new DoubleAnimationUsingKeyFrames { Duration = duration };
        arc.KeyFrames.Add(new EasingDoubleKeyFrame(-70, KeyTime.FromPercent(0.4), new CubicEase { EasingMode = EasingMode.EaseOut }));
        arc.KeyFrames.Add(new EasingDoubleKeyFrame(PilePt.Y - CameraPt.Y + 10, KeyTime.FromPercent(1), new CubicEase { EasingMode = EasingMode.EaseIn }));
        arc.Completed += (_, _) => { Fx.Children.Remove(mini); AddToPile(imported); };
        moveY.BeginAnimation(TranslateTransform.YProperty, arc);
        rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(rotate.Angle + _random.Next(200, 400), duration));
        Sparkle(CameraPt.X - 4, CameraPt.Y - 30, Colors.White, 9, 10, 0.5);
    }

    private void AddToPile(int imported)
    {
        if (Pile.Children.Count >= 8) Pile.Children.RemoveAt(0);
        var mini = MiniPolaroid(1.15);
        var pop = new ScaleTransform(1.3, 1.3);
        mini.RenderTransform = new TransformGroup { Children = { pop, new RotateTransform(_random.Next(-25, 25)) } };
        Canvas.SetLeft(mini, 30 + _random.Next(-16, 16));
        Canvas.SetTop(mini, 36 - Math.Min(Pile.Children.Count, 7) * 3);
        Pile.Children.Add(mini);
        var ease = new BackEase { Amplitude = 0.6, EasingMode = EasingMode.EaseOut };
        pop.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        pop.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        PileText.Text = $"已收 {Math.Max(imported, _lastSuccess):N0}";
        if (PileBadge.Opacity < 0.5) PileBadge.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(250)));
    }

    private void ClearPile()
    {
        Pile.Children.Clear();
        PileBadge.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)));
    }

    private void Celebrate()
    {
        var flash = new DoubleAnimationUsingKeyFrames();
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
        flash.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900)), new CubicEase { EasingMode = EasingMode.EaseOut }));
        Flash.BeginAnimation(OpacityProperty, flash);
        Stamp.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { BeginTime = TimeSpan.FromMilliseconds(350) });
        var ease = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut };
        StampScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(2, 1, TimeSpan.FromMilliseconds(260)) { BeginTime = TimeSpan.FromMilliseconds(350), EasingFunction = ease });
        StampScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(2, 1, TimeSpan.FromMilliseconds(260)) { BeginTime = TimeSpan.FromMilliseconds(350), EasingFunction = ease });
        Hop();
        if (_calm || _peek) return;
        for (var i = 0; i < 14; i++)
        {
            if (i % 2 == 1)
            {
                Sparkle(PolPt.X + _random.Next(-40, 40), PolPt.Y + _random.Next(-50, 20), i % 4 == 1 ? _outfit.Accent : Colors.White, 6 + _random.Next(8), 40, 1.3);
                continue;
            }
            var mini = MiniPolaroid(0.8);
            var rotate = new RotateTransform(_random.Next(-40, 40));
            var moveX = new TranslateTransform();
            var moveY = new TranslateTransform();
            mini.RenderTransform = new TransformGroup { Children = { rotate, moveX, moveY } };
            Canvas.SetLeft(mini, PolPt.X);
            Canvas.SetTop(mini, PolPt.Y - 40);
            Fx.Children.Add(mini);
            var duration = TimeSpan.FromMilliseconds(1300 + _random.Next(400));
            moveX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(_random.Next(-150, 150), duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            var arc = new DoubleAnimationUsingKeyFrames { Duration = duration };
            arc.KeyFrames.Add(new EasingDoubleKeyFrame(-_random.Next(80, 170), KeyTime.FromPercent(0.4), new CubicEase { EasingMode = EasingMode.EaseOut }));
            arc.KeyFrames.Add(new EasingDoubleKeyFrame(120, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
            var opacity = new DoubleAnimationUsingKeyFrames { Duration = duration };
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.7)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            opacity.Completed += (_, _) => Fx.Children.Remove(mini);
            moveY.BeginAnimation(TranslateTransform.YProperty, arc);
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(rotate.Angle + _random.Next(-360, 360), duration));
            mini.BeginAnimation(OpacityProperty, opacity);
        }
    }

    private void Hearts(int count)
    {
        for (var i = 0; i < count; i++)
        {
            After(i * 0.15, () => Glyph("♥", HeadPt.X + _random.Next(-50, 30), HeadPt.Y - 40, _outfit.Accent, 14 + _random.Next(8), 50, 1.4, _random.Next(-20, 20)));
        }
    }

    // ===================== per-state effects =====================
    private void ClearStateFx()
    {
        foreach (var element in _stateFx) Fx.Children.Remove(element);
        _stateFx.Clear();
    }

    private void AddStateFx(UIElement element)
    {
        _stateFx.Add(element);
        Fx.Children.Add(element);
    }

    private void StartStateFx(string key)
    {
        if (_calm) return;
        switch (key)
        {
            case "Scanning":
            {
                var frame = new Grid { Width = 128, Height = 108, RenderTransformOrigin = new Point(0.5, 0.5), Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 0, Opacity = 0.8, Color = Colors.Black } };
                foreach (var (h, v, data) in new[]
                         {
                             (HorizontalAlignment.Left, VerticalAlignment.Top, "M0,14 L0,0 L14,0"),
                             (HorizontalAlignment.Right, VerticalAlignment.Top, "M0,0 L14,0 L14,14"),
                             (HorizontalAlignment.Left, VerticalAlignment.Bottom, "M0,0 L0,14 L14,14"),
                             (HorizontalAlignment.Right, VerticalAlignment.Bottom, "M14,0 L14,14 L0,14")
                         })
                {
                    frame.Children.Add(new Path { Data = Geometry.Parse(data), Stroke = Brushes.White, StrokeThickness = 2.5, HorizontalAlignment = h, VerticalAlignment = v, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
                }
                var rec = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Hex("#FF5A6E")), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 24, 0) };
                frame.Children.Add(rec);
                var pulse = new ScaleTransform();
                frame.RenderTransform = pulse;
                Canvas.SetLeft(frame, HeadPt.X - 64);
                Canvas.SetTop(frame, HeadPt.Y - 64);
                AddStateFx(frame);
                Loop(pulse, ScaleTransform.ScaleXProperty, 1, 0.9, 0.7, new SineEase());
                Loop(pulse, ScaleTransform.ScaleYProperty, 1, 0.9, 0.7, new SineEase());
                rec.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.1, TimeSpan.FromMilliseconds(500)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
                break;
            }
            case "Checking":
            {
                var glass = new Grid { Width = 40, Height = 40, Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.6 } };
                glass.Children.Add(new Ellipse { Width = 26, Height = 26, Stroke = Brushes.White, StrokeThickness = 3, Fill = new SolidColorBrush(Color.FromArgb(0x44, 0xBF, 0xE3, 0xFF)), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
                glass.Children.Add(new Path { Data = Geometry.Parse("M22,22 L36,36"), Stroke = Brushes.White, StrokeThickness = 4, StrokeEndLineCap = PenLineCap.Round });
                var move = new TranslateTransform();
                glass.RenderTransform = move;
                Canvas.SetLeft(glass, PolPt.X - 50);
                Canvas.SetTop(glass, PolPt.Y - 110);
                AddStateFx(glass);
                Loop(move, TranslateTransform.XProperty, 0, 70, 1.3, new SineEase { EasingMode = EasingMode.EaseInOut });
                Loop(move, TranslateTransform.YProperty, 0, -10, 0.65, new SineEase { EasingMode = EasingMode.EaseInOut });
                break;
            }
            case "Interrupted":
            {
                var mark = new TextBlock { Text = "?", FontSize = 30, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Hex("#FFB866")), Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.6 } };
                var bounce = new TranslateTransform();
                mark.RenderTransform = bounce;
                Canvas.SetLeft(mark, HeadPt.X + 46);
                Canvas.SetTop(mark, HeadPt.Y - 92);
                AddStateFx(mark);
                Loop(bounce, TranslateTransform.YProperty, 0, -8, 0.45, new QuadraticEase { EasingMode = EasingMode.EaseOut });
                Shake();
                break;
            }
            case "StopRequested":
            {
                var dots = new TextBlock { Text = "…", FontSize = 26, FontWeight = FontWeights.Black, Foreground = Brushes.White, Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.6 } };
                Canvas.SetLeft(dots, HeadPt.X + 40);
                Canvas.SetTop(dots, HeadPt.Y - 88);
                AddStateFx(dots);
                dots.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.2, TimeSpan.FromMilliseconds(500)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
                break;
            }
            case "Failed":
            case "Error":
            case "CompletedWithIssues":
                Shake();
                break;
        }
    }

    private void AmbientTick(object? sender, EventArgs e)
    {
        if (_peek || _calm || !IsVisible) return;
        var rate = _tone == "active" ? 0.22 : _stateKey == "Idle" ? 0.07 : 0.1;
        if (_random.NextDouble() < rate)
        {
            var color = _random.NextDouble() < 0.5 ? Colors.White : _outfit.Accent;
            Sparkle(SurfaceWidth - 250 + _random.Next(0, 240), SurfaceHeight - 190 + _random.Next(0, 150), color, 3 + _random.Next(5), 18 + _random.Next(20), 1.0 + _random.NextDouble());
        }
        if (_tone == "active" && _random.NextDouble() < 0.06) Sparkle(CameraPt.X, CameraPt.Y - 26, Colors.White, 11, 4, 0.45);
        if (_stateKey is "Failed" or "Error" && _random.NextDouble() < 0.12) Puff(CameraPt.X - 6, CameraPt.Y - 30);
        if (_stateKey == "Recoverable" && _random.NextDouble() < 0.05) Sparkle(PolPt.X + 60, PolPt.Y - 70, Hex("#8FB8FF"), 7, 14, 1.0);
        if (_stateKey == "Idle" && (DateTime.Now - _lastInteract).TotalSeconds > 45 && _random.NextDouble() < 0.03)
        {
            Glyph("z", HeadPt.X + 40 + _random.Next(0, 10), HeadPt.Y - 70, Colors.White, 12 + _random.Next(8), 40, 2.2, 18);
        }
    }

    // ===================== input =====================
    private void Char_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _lastInteract = DateTime.Now;
        e.Handled = true;
        if (e.ClickCount >= 2)
        {
            RestoreMainWindow();
            return;
        }
        var left = Left;
        var top = Top;
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        if (Math.Abs(Left - left) < 3 && Math.Abs(Top - top) < 3)
        {
            Poke();
            return;
        }
        _hasUserPosition = true;
        var area = SystemParameters.WorkArea;
        if (Left + Width > area.Right - 40 * _scale) { CollapseToPeek(); return; }
        if (Top + Height > area.Bottom - 60 && Top + Height < area.Bottom + 200) Top = area.Bottom - Height;
    }

    private void Poke()
    {
        Hop();
        if (!_calm)
        {
            Hearts(3);
            for (var i = 0; i < 4; i++) Sparkle(HeadPt.X + _random.Next(-60, 60), HeadPt.Y + _random.Next(-50, 20), Colors.White, 5 + _random.Next(5));
        }
        if (_raised) return;
        if (_stateKey == "Idle")
        {
            var pool = _outfit.Lines.Concat(["嗯？叫我吗～", "别戳啦，镜头会花的！", "双击我就打开 Hanabe 哦。"]).ToArray();
            Say(pool[_random.Next(pool.Length)], null, 3.5);
        }
        else
        {
            Raise();
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e) => CollapseToPeek();

    // ===================== peek =====================
    private void CollapseToPeek()
    {
        if (_peek) return;
        Lower();
        HideBubble();
        _peek = true;
        _savedLeft = Left;
        _savedTop = Top;
        Stage.Visibility = Visibility.Collapsed;
        Fx.Children.Clear();
        _stateFx.Clear();
        Peek.Visibility = Visibility.Visible;
        ApplyWindowSize();
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width;
        Top = area.Top + area.Height * 0.42;
        Animate(PeekSlide, TranslateTransform.XProperty, 0, TimeSpan.FromMilliseconds(520), new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }, 120);
        RefreshPeekVisibility();
        _peekNudge.Start();
    }

    private void RefreshPeekVisibility()
    {
        if (_calm) { PeekGlow.BeginAnimation(OpacityProperty, null); PeekGlow.Opacity = 0.7; return; }
        PeekGlow.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.45, TimeSpan.FromSeconds(1.2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }

    private void PeekNudge()
    {
        if (!_peek || _calm) return;
        var nudge = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1600) };
        nudge.KeyFrames.Add(new EasingDoubleKeyFrame(-26, KeyTime.FromPercent(0.25), new BackEase { EasingMode = EasingMode.EaseOut }));
        nudge.KeyFrames.Add(new LinearDoubleKeyFrame(-26, KeyTime.FromPercent(0.7)));
        nudge.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new CubicEase { EasingMode = EasingMode.EaseInOut }));
        PeekSlide.BeginAnimation(TranslateTransform.XProperty, nudge);
    }

    private void Peek_MouseEnter(object sender, MouseEventArgs e) =>
        Animate(PeekSlide, TranslateTransform.XProperty, -30, TimeSpan.FromMilliseconds(260), new BackEase { EasingMode = EasingMode.EaseOut });

    private void Peek_MouseLeave(object sender, MouseEventArgs e) =>
        Animate(PeekSlide, TranslateTransform.XProperty, 0, TimeSpan.FromMilliseconds(300), new CubicEase { EasingMode = EasingMode.EaseInOut });

    private void Peek_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => RestoreFromPeek();

    private void RestoreFromPeek()
    {
        if (!_peek) return;
        _peek = false;
        _peekNudge.Stop();
        Peek.Visibility = Visibility.Collapsed;
        Stage.Visibility = Visibility.Visible;
        ApplyWindowSize();
        var area = SystemParameters.WorkArea;
        Left = Math.Min(_savedLeft, area.Right - Width - 16);
        Top = Math.Min(_savedTop, area.Bottom - Height);
        Hop();
        if (!_calm) Hearts(2);
        StartStateFx(_stateKey);
        if (Looks.TryGetValue(_stateKey, out var look)) Say(_stateKey == "Idle" ? "我回来啦～" : look.Bubble, null, _tone == "active" ? 0 : 5);
        if (_persistent) After(0.6, Raise);
    }

    // ===================== window / helpers =====================
    private void ApplyWindowSize()
    {
        var width = _peek ? PeekWidth : SurfaceWidth;
        var height = _peek ? PeekHeight : SurfaceHeight;
        Surface.Width = width;
        Surface.Height = height;
        Width = width * _scale;
        Height = height * _scale;
        PositionAutomatically();
    }

    private void PositionAutomatically()
    {
        if (_hasUserPosition || _peek) return;
        var area = SystemParameters.WorkArea;
        Left = Math.Max(area.Left, area.Right - Width - 16);
        Top = Math.Max(area.Top, area.Bottom - Height);
    }

    private void StartLoops()
    {
        if (_calm)
        {
            BustY.BeginAnimation(TranslateTransform.YProperty, null);
            BustSway.BeginAnimation(RotateTransform.AngleProperty, null);
            FilmShimmerX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-60, 150, TimeSpan.FromSeconds(2.4)) { RepeatBehavior = RepeatBehavior.Forever });
            return;
        }
        Loop(BustY, TranslateTransform.YProperty, 0, -3.5, 2.1, new SineEase { EasingMode = EasingMode.EaseInOut });
        Loop(BustSway, RotateTransform.AngleProperty, -1.3, 1.3, 2.6, new SineEase { EasingMode = EasingMode.EaseInOut });
        Loop(BookmarkSwing, RotateTransform.AngleProperty, -8, 8, 1.1, new SineEase { EasingMode = EasingMode.EaseInOut });
        Loop(PeekBob, TranslateTransform.XProperty, 0, -5, 1.6, new SineEase { EasingMode = EasingMode.EaseInOut });
        FilmShimmerX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-60, 150, TimeSpan.FromSeconds(1.5)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
    }

    private static void Loop(Animatable target, DependencyProperty property, double from, double to, double seconds, IEasingFunction ease) =>
        target.BeginAnimation(property, new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });

    private static void Animate(Animatable target, DependencyProperty property, double to, TimeSpan duration, IEasingFunction? ease, double? from = null)
    {
        var animation = from.HasValue ? new DoubleAnimation(from.Value, to, duration) : new DoubleAnimation(to, duration);
        animation.EasingFunction = ease;
        target.BeginAnimation(property, animation);
    }

    private void Hop()
    {
        var hop = new DoubleAnimationUsingKeyFrames();
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(0.94, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(1.04, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(210))));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
        BustScale.BeginAnimation(ScaleTransform.ScaleYProperty, hop);
    }

    private void Shake()
    {
        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(420) };
        double[] offsets = [-4, 4, -3, 3, -1.5, 0];
        for (var i = 0; i < offsets.Length; i++) shake.KeyFrames.Add(new LinearDoubleKeyFrame(offsets[i], KeyTime.FromPercent((i + 1) / (double)offsets.Length)));
        CharShake.BeginAnimation(TranslateTransform.XProperty, shake);
    }

    private void After(double seconds, Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (_, _) => { timer.Stop(); action(); };
        timer.Start();
    }

    private static Color Hex(string value) => (Color)ColorConverter.ConvertFromString(value);

    private static void RestoreMainWindow()
    {
        if (Application.Current.MainWindow is not { } main) return;
        main.Show();
        main.WindowState = WindowState.Normal;
        main.Activate();
    }
}
