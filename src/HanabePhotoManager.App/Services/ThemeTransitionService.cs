using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using WpfImage = System.Windows.Controls.Image;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace HanabePhotoManager.App.Services;

/// <summary>
/// 以点击位置为圆心的深浅色切换：先截下旧主题画面盖在最上层，再切换主题，
/// 等新主题真正渲染出第一帧后，用带柔边的圆形渐变遮罩把旧画面“化开”，最后整体淡出。
/// 柔边 + 等首帧 + 正弦缓动，避免旧版硬边裁剪和切换瞬间卡帧造成的生硬感。
/// </summary>
public static class ThemeTransitionService
{
    private static readonly TimeSpan RevealDuration = TimeSpan.FromMilliseconds(620);
    private const double FeatherFraction = 0.22;
    private static bool isAnimating;

    public static void Apply(Window window, FrameworkElement sourceElement, Action applyTheme)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(sourceElement);
        ArgumentNullException.ThrowIfNull(applyTheme);

        if (isAnimating || !SystemParameters.ClientAreaAnimation || window.ActualWidth <= 0 || window.ActualHeight <= 0 ||
            window.Content is not AdornerDecorator decorator || decorator.Child is not FrameworkElement source)
        {
            applyTheme();
            return;
        }

        // 侧栏收起时切换按钮不可见：从窗口左上角附近展开。
        var origin = sourceElement.IsVisible && sourceElement.ActualWidth > 0
            ? sourceElement.TranslatePoint(new WpfPoint(sourceElement.ActualWidth / 2, sourceElement.ActualHeight / 2), source)
            : new WpfPoint(48, 48);
        var dpi = VisualTreeHelper.GetDpi(source);
        var snapshot = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(source.RenderSize.Width * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(source.RenderSize.Height * dpi.DpiScaleY)),
            96 * dpi.DpiScaleX,
            96 * dpi.DpiScaleY,
            PixelFormats.Pbgra32);
        snapshot.Render(source);
        snapshot.Freeze();

        // 旧画面先盖住，再切主题：用户看不到切换瞬间的重排和闪烁。
        var layer = decorator.AdornerLayer;
        var adorner = new ThemeRevealAdorner(source, snapshot, origin);
        isAnimating = true;
        layer.Add(adorner);
        applyTheme();

        // 等新主题渲染完一帧再开始动画，开头的几帧不会因为重绘而被吞掉。
        EventHandler? onRendering = null;
        var frames = 0;
        onRendering = (_, _) =>
        {
            if (++frames < 2) return;
            CompositionTarget.Rendering -= onRendering;
            adorner.Begin(() =>
            {
                layer.Remove(adorner);
                isAnimating = false;
            });
        };
        CompositionTarget.Rendering += onRendering;
    }

    private sealed class ThemeRevealAdorner : Adorner
    {
        private readonly WpfImage snapshotImage;
        private readonly RadialGradientBrush mask;
        private readonly WpfPoint origin;

        public ThemeRevealAdorner(UIElement adornedElement, ImageSource snapshot, WpfPoint origin)
            : base(adornedElement)
        {
            IsHitTestVisible = false;
            this.origin = origin;
            // 遮罩：圆心透明（露出新主题），圆外不透明（仍是旧画面），中间一圈柔边过渡。
            mask = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                Center = origin,
                GradientOrigin = origin,
                RadiusX = 0.1,
                RadiusY = 0.1,
                GradientStops =
                {
                    new GradientStop(Colors.Transparent, 0),
                    new GradientStop(Colors.Transparent, 1 - FeatherFraction),
                    new GradientStop(Colors.Black, 1),
                },
            };
            snapshotImage = new WpfImage
            {
                Source = snapshot,
                Stretch = Stretch.Fill,
                OpacityMask = mask,
            };
            AddVisualChild(snapshotImage);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => index == 0 ? snapshotImage : throw new ArgumentOutOfRangeException(nameof(index));
        protected override WpfSize ArrangeOverride(WpfSize finalSize)
        {
            snapshotImage.Arrange(new Rect(finalSize));
            return finalSize;
        }

        public void Begin(Action completed)
        {
            var bounds = AdornedElement.RenderSize;
            var farthest = new[]
            {
                (origin - new WpfPoint(0, 0)).Length,
                (origin - new WpfPoint(bounds.Width, 0)).Length,
                (origin - new WpfPoint(0, bounds.Height)).Length,
                (origin - new WpfPoint(bounds.Width, bounds.Height)).Length,
            }.Max();
            // 柔边内沿也要越过最远的角，才算完全展开。
            var radius = farthest / (1 - FeatherFraction) + 4;
            var easing = new SineEase { EasingMode = EasingMode.EaseInOut };
            var grow = new DoubleAnimation(0.1, radius, RevealDuration) { EasingFunction = easing };
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(RevealDuration.TotalMilliseconds * 0.35))
            {
                BeginTime = TimeSpan.FromMilliseconds(RevealDuration.TotalMilliseconds * 0.65),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            fade.Completed += (_, _) => completed();
            mask.BeginAnimation(RadialGradientBrush.RadiusXProperty, grow);
            mask.BeginAnimation(RadialGradientBrush.RadiusYProperty, grow.Clone());
            snapshotImage.BeginAnimation(OpacityProperty, fade);
        }
    }
}
