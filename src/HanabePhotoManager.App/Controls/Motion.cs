using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HanabePhotoManager.App.Controls;

/// <summary>
/// 全局统一的动效节奏：
/// · RevealOrder：元素每次变为可见时淡入 + 轻微上浮，按序号错开 40ms（最多错开 8 级），形成从上到下的层次；
/// · HoverLift：鼠标悬停时轻微放大上浮，离开时回落。
/// Windows “动画效果”关闭时全部跳过，直接显示最终状态。
/// </summary>
public static class Motion
{
    public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan Normal = TimeSpan.FromMilliseconds(220);
    public static readonly TimeSpan Emphasized = TimeSpan.FromMilliseconds(340);
    public static readonly TimeSpan Stagger = TimeSpan.FromMilliseconds(40);
    private const int MaxStaggerSteps = 8;
    private const double RevealDistance = 12;

    public static IEasingFunction EaseOut { get; } = Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });
    public static IEasingFunction EaseInOut { get; } = Freeze(new SineEase { EasingMode = EasingMode.EaseInOut });

    public static bool IsEnabled => SystemParameters.ClientAreaAnimation;

    // ---------- RevealOrder ----------

    public static readonly DependencyProperty RevealOrderProperty = DependencyProperty.RegisterAttached(
        "RevealOrder", typeof(int), typeof(Motion), new PropertyMetadata(-1, OnRevealOrderChanged));

    public static int GetRevealOrder(DependencyObject element) => (int)element.GetValue(RevealOrderProperty);
    public static void SetRevealOrder(DependencyObject element, int value) => element.SetValue(RevealOrderProperty, value);

    private static void OnRevealOrderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        element.IsVisibleChanged -= OnRevealVisibleChanged;
        if ((int)e.NewValue >= 0) element.IsVisibleChanged += OnRevealVisibleChanged;
    }

    private static void OnRevealVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is FrameworkElement element && e.NewValue is true) Reveal(element, GetRevealOrder(element));
    }

    /// <summary>播放一次入场：淡入 + 上浮。可重复调用，新动画会打断旧动画。</summary>
    public static void Reveal(FrameworkElement element, int order = 0)
    {
        if (!IsEnabled) return;
        var delay = TimeSpan.FromMilliseconds(Stagger.TotalMilliseconds * Math.Clamp(order, 0, MaxStaggerSteps));
        var (_, translate) = EnsureTransforms(element);

        // 先把基础值设为起点，避免延迟期间元素先闪现再消失。
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 0;
        var fade = new DoubleAnimation(0, 1, Emphasized) { BeginTime = delay, EasingFunction = EaseOut };
        fade.Completed += (_, _) =>
        {
            element.Opacity = 1;
            element.BeginAnimation(UIElement.OpacityProperty, null);
        };
        element.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);

        if (translate is not null)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.Y = RevealDistance;
            var rise = new DoubleAnimation(RevealDistance, 0, Emphasized) { BeginTime = delay, EasingFunction = EaseOut };
            rise.Completed += (_, _) =>
            {
                translate.Y = 0;
                translate.BeginAnimation(TranslateTransform.YProperty, null);
            };
            translate.BeginAnimation(TranslateTransform.YProperty, rise, HandoffBehavior.SnapshotAndReplace);
        }
    }

    // ---------- HoverLift ----------

    public static readonly DependencyProperty HoverLiftProperty = DependencyProperty.RegisterAttached(
        "HoverLift", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnHoverLiftChanged));

    public static bool GetHoverLift(DependencyObject element) => (bool)element.GetValue(HoverLiftProperty);
    public static void SetHoverLift(DependencyObject element, bool value) => element.SetValue(HoverLiftProperty, value);

    private static void OnHoverLiftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        element.MouseEnter -= OnHoverEnter;
        element.MouseLeave -= OnHoverLeave;
        if (e.NewValue is true)
        {
            element.MouseEnter += OnHoverEnter;
            element.MouseLeave += OnHoverLeave;
        }
    }

    private static void OnHoverEnter(object sender, System.Windows.Input.MouseEventArgs e) => Lift(sender as FrameworkElement, 1.03, -2);
    private static void OnHoverLeave(object sender, System.Windows.Input.MouseEventArgs e) => Lift(sender as FrameworkElement, 1, 0);

    private static void Lift(FrameworkElement? element, double scaleTo, double offsetTo)
    {
        if (element is null || !IsEnabled) return;
        var (scale, _) = EnsureTransforms(element);
        if (scale is null) return;
        element.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        var duration = scaleTo > 1 ? Fast : Normal;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scaleTo, duration) { EasingFunction = EaseOut }, HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scaleTo, duration) { EasingFunction = EaseOut }, HandoffBehavior.SnapshotAndReplace);
        // 上浮用单独的 TranslateTransform 子项，不与入场动画争用同一个属性。
        if (element.RenderTransform is TransformGroup { Children.Count: 3 } group && group.Children[2] is TranslateTransform hover)
            hover.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offsetTo, duration) { EasingFunction = EaseOut }, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// 统一的变换组：[0] 悬停缩放、[1] 入场位移、[2] 悬停位移。
    /// 元素已有自己的 RenderTransform 时不覆盖，只做透明度动画。
    /// </summary>
    private static (ScaleTransform? Scale, TranslateTransform? Translate) EnsureTransforms(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup { Children.Count: 3 } existing &&
            existing.Children[0] is ScaleTransform s && existing.Children[1] is TranslateTransform t &&
            existing.Children[2] is TranslateTransform)
        {
            return (s, t);
        }

        if (element.RenderTransform is not null && !ReferenceEquals(element.RenderTransform, Transform.Identity))
        {
            return (null, null);
        }

        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform();
        element.RenderTransform = new TransformGroup { Children = { scale, translate, new TranslateTransform() } };
        return (scale, translate);
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
