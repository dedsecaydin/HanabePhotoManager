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

public static class ThemeTransitionService
{
    private static bool isAnimating;

    public static void Apply(Window window, FrameworkElement sourceElement, Action applyTheme)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(sourceElement);
        ArgumentNullException.ThrowIfNull(applyTheme);

        if (isAnimating || window.ActualWidth <= 0 || window.ActualHeight <= 0 || window.Content is not AdornerDecorator decorator)
        {
            applyTheme();
            return;
        }

        var source = decorator.Child;
        var origin = sourceElement.TranslatePoint(
            new WpfPoint(sourceElement.ActualWidth / 2, sourceElement.ActualHeight / 2),
            source);
        var dpi = VisualTreeHelper.GetDpi(source);
        var snapshot = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(source.RenderSize.Width * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(source.RenderSize.Height * dpi.DpiScaleY)),
            96 * dpi.DpiScaleX,
            96 * dpi.DpiScaleY,
            PixelFormats.Pbgra32);
        snapshot.Render(source);
        snapshot.Freeze();

        applyTheme();
        window.UpdateLayout();

        var layer = decorator.AdornerLayer;
        var adorner = new ThemeRevealAdorner(source, snapshot, origin);
        isAnimating = true;
        layer.Add(adorner);
        adorner.Begin(() =>
        {
            layer.Remove(adorner);
            isAnimating = false;
        });
    }

    private sealed class ThemeRevealAdorner : Adorner
    {
        private readonly WpfImage snapshotImage;
        private readonly EllipseGeometry revealCircle;

        public ThemeRevealAdorner(UIElement adornedElement, ImageSource snapshot, WpfPoint origin)
            : base(adornedElement)
        {
            IsHitTestVisible = false;
            revealCircle = new EllipseGeometry(origin, 0, 0);
            var outsideCircle = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                new RectangleGeometry(new Rect(new WpfPoint(), adornedElement.RenderSize)),
                revealCircle);
            snapshotImage = new WpfImage
            {
                Source = snapshot,
                Stretch = Stretch.Fill,
                Clip = outsideCircle
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
            var origin = revealCircle.Center;
            var bounds = AdornedElement.RenderSize;
            var radius = new[]
            {
                (origin - new WpfPoint(0, 0)).Length,
                (origin - new WpfPoint(bounds.Width, 0)).Length,
                (origin - new WpfPoint(0, bounds.Height)).Length,
                (origin - new WpfPoint(bounds.Width, bounds.Height)).Length
            }.Max() + 2;
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var animation = new DoubleAnimation(0, radius, TimeSpan.FromMilliseconds(420)) { EasingFunction = easing };
            animation.Completed += (_, _) => completed();
            revealCircle.BeginAnimation(EllipseGeometry.RadiusXProperty, animation);
            revealCircle.BeginAnimation(EllipseGeometry.RadiusYProperty, animation.Clone());
        }
    }
}
