using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HanabePhotoManager.App;

/// <summary>
/// 查看器的过渡动画：打开时内容从 94% 放大并淡入（视频画面等动画结束再出现，避免 VLC 子窗口跳出来），
/// 关闭时缩回 96% 并淡出后才真正关窗；左右切换照片时新照片轻微淡入。
/// 系统关闭了“窗口内动画”（SystemParameters.ClientAreaAnimation）时全部跳过。
/// </summary>
public partial class PhotoViewerWindow
{
    internal static readonly Duration OpenDuration = TimeSpan.FromMilliseconds(260);
    internal static readonly Duration CloseDuration = TimeSpan.FromMilliseconds(160);
    internal static readonly Duration SwapDuration = TimeSpan.FromMilliseconds(140);

    private readonly TaskCompletionSource _openTransition = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _exitTransitionDone;
    private bool _exitTransitionRunning;
    private ScaleTransform? _rootScale;

    private static bool MotionEnabled => SystemParameters.ClientAreaAnimation;

    /// <summary>打开动画结束（或被跳过）后完成；视频在这之后才把画面窗口显示出来。</summary>
    private Task OpenTransitionCompleted => _openTransition.Task;

    private ScaleTransform RootScale()
    {
        if (_rootScale is not null) return _rootScale;
        _rootScale = new ScaleTransform(1, 1);
        ViewerRoot.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        ViewerRoot.RenderTransform = _rootScale;
        return _rootScale;
    }

    private void PlayOpenTransition()
    {
        if (!MotionEnabled)
        {
            _openTransition.TrySetResult();
            return;
        }

        var scale = RootScale();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ViewerRoot.Opacity = 0;
        scale.ScaleX = scale.ScaleY = 0.94;
        var fade = new DoubleAnimation(0, 1, OpenDuration) { EasingFunction = ease };
        fade.Completed += (_, _) =>
        {
            ViewerRoot.BeginAnimation(OpacityProperty, null);
            ViewerRoot.Opacity = 1;
            _openTransition.TrySetResult();
        };
        var grow = new DoubleAnimation(0.94, 1, OpenDuration) { EasingFunction = ease };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        ViewerRoot.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>
    /// 关窗前先播放退出动画：第一次 Closing 取消并开始动画，动画结束再真正 Close。
    /// 返回 true 表示这次 Closing 已被拦下（调用方应直接 return）。
    /// </summary>
    private bool InterceptCloseForTransition(CancelEventArgs e)
    {
        if (_exitTransitionDone || !MotionEnabled || !IsLoaded) return false;
        e.Cancel = true;
        if (_exitTransitionRunning) return true;
        _exitTransitionRunning = true;

        // 视频画面是 Win32 子窗口，不受 WPF 透明度影响：先停掉画面再收起内容。
        if (_videoActive)
        {
            _mediaPlayer?.Pause();
            VideoHost.Visibility = Visibility.Collapsed;
        }

        var scale = RootScale();
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(ViewerRoot.Opacity, 0, CloseDuration) { EasingFunction = ease };
        fade.Completed += (_, _) =>
        {
            _exitTransitionDone = true;
            Dispatcher.BeginInvoke(new Action(Close));
        };
        var shrink = new DoubleAnimation(scale.ScaleX, 0.96, CloseDuration) { EasingFunction = ease };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        ViewerRoot.BeginAnimation(OpacityProperty, fade);
        return true;
    }

    /// <summary>左右切换到另一张照片：新照片从 98.5% 轻微放大并淡入。</summary>
    private void PlayPhotoSwapTransition()
    {
        if (!MotionEnabled || !OpenTransitionCompleted.IsCompleted) return;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        if (PhotoImage.RenderTransform is not ScaleTransform imageScale || imageScale.IsFrozen)
        {
            imageScale = new ScaleTransform(1, 1);
            PhotoImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            PhotoImage.RenderTransform = imageScale;
        }
        var grow = new DoubleAnimation(0.985, 1, SwapDuration) { EasingFunction = ease };
        imageScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        imageScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        PhotoImage.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, SwapDuration) { EasingFunction = ease });
    }
}
