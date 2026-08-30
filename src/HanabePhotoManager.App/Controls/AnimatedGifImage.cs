using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;

namespace HanabePhotoManager.App.Controls;

internal static class GifFrameTiming
{
    internal static TimeSpan NormalizeDelay(int centiseconds) =>
        centiseconds <= 0 ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromMilliseconds(Math.Max(20, centiseconds * 10));
}

public sealed class AnimatedGifImage : System.Windows.Controls.Image
{
    public static readonly DependencyProperty AnimationSourceProperty = DependencyProperty.Register(
        nameof(AnimationSource), typeof(string), typeof(AnimatedGifImage), new PropertyMetadata(null, OnAnimationSourceChanged));
    public static readonly DependencyProperty FallbackSourceProperty = DependencyProperty.Register(
        nameof(FallbackSource), typeof(string), typeof(AnimatedGifImage), new PropertyMetadata(null));
    public static readonly DependencyProperty UseNearestNeighborProperty = DependencyProperty.Register(
        nameof(UseNearestNeighbor), typeof(bool), typeof(AnimatedGifImage), new PropertyMetadata(false, OnScalingChanged));

    private readonly DispatcherTimer _timer = new();
    private IReadOnlyList<BitmapFrame> _frames = [];
    private IReadOnlyList<TimeSpan> _delays = [];
    private int _frameIndex;

    public AnimatedGifImage()
    {
        _timer.Tick += (_, _) => AdvanceFrame();
        Loaded += (_, _) => { LoadAnimation(); UpdatePlayback(); };
        Unloaded += (_, _) => _timer.Stop();
        IsVisibleChanged += (_, _) => UpdatePlayback();
    }

    public string? AnimationSource { get => (string?)GetValue(AnimationSourceProperty); set => SetValue(AnimationSourceProperty, value); }
    public string? FallbackSource { get => (string?)GetValue(FallbackSourceProperty); set => SetValue(FallbackSourceProperty, value); }
    public bool UseNearestNeighbor { get => (bool)GetValue(UseNearestNeighborProperty); set => SetValue(UseNearestNeighborProperty, value); }

    private static void OnAnimationSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((AnimatedGifImage)sender).LoadAnimation();
    private static void OnScalingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        RenderOptions.SetBitmapScalingMode((AnimatedGifImage)sender, (bool)e.NewValue ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);

    private void LoadAnimation()
    {
        _timer.Stop();
        _frames = [];
        _delays = [];
        _frameIndex = 0;
        try
        {
            if (string.IsNullOrWhiteSpace(AnimationSource)) { ShowFallback(); return; }
            var resource = System.Windows.Application.GetResourceStream(new Uri(AnimationSource, UriKind.Relative));
            if (resource is null) { ShowFallback(); return; }
            using var stream = resource.Stream;
            var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            _frames = decoder.Frames.ToArray();
            _delays = _frames.Select(ReadDelay).ToArray();
            if (_frames.Count == 0) { ShowFallback(); return; }
            Source = _frames[0];
            UpdatePlayback();
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException)
        {
            ShowFallback();
        }
    }

    private static TimeSpan ReadDelay(BitmapFrame frame)
    {
        try
        {
            var metadata = frame.Metadata as BitmapMetadata;
            var value = metadata?.GetQuery("/grctlext/Delay");
            return GifFrameTiming.NormalizeDelay(value is ushort delay ? delay : 0);
        }
        catch (NotSupportedException) { return GifFrameTiming.NormalizeDelay(0); }
    }

    private void AdvanceFrame()
    {
        if (_frames.Count < 2) { _timer.Stop(); return; }
        _frameIndex = (_frameIndex + 1) % _frames.Count;
        Source = _frames[_frameIndex];
        _timer.Interval = _delays[_frameIndex];
    }

    private void UpdatePlayback()
    {
        if (!IsLoaded || !IsVisible || _frames.Count < 2 || !SystemParameters.ClientAreaAnimation) { _timer.Stop(); return; }
        _timer.Interval = _delays[_frameIndex];
        _timer.Start();
    }

    private void ShowFallback()
    {
        if (string.IsNullOrWhiteSpace(FallbackSource)) { Source = null; return; }
        Source = new BitmapImage(new Uri(FallbackSource, UriKind.Relative));
    }
}
