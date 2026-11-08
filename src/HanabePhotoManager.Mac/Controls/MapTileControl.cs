using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;

namespace HanabePhotoManager.Mac.Controls;

/// <summary>Interactive raster map; geographic calculations and tile caching are shared.</summary>
public sealed class MapTileControl : Control
{
    public static readonly StyledProperty<double> LatitudeProperty = AvaloniaProperty.Register<MapTileControl, double>(nameof(Latitude), 0);
    public static readonly StyledProperty<double> LongitudeProperty = AvaloniaProperty.Register<MapTileControl, double>(nameof(Longitude), 0);
    public static readonly StyledProperty<int> ZoomProperty = AvaloniaProperty.Register<MapTileControl, int>(nameof(Zoom), 2, coerce: (_, value) => Math.Clamp(value, 0, 19));
    public static readonly StyledProperty<IEnumerable<MapMarkerPayload>?> MarkersProperty = AvaloniaProperty.Register<MapTileControl, IEnumerable<MapMarkerPayload>?>(nameof(Markers));
    private static readonly HttpClient Http = new();
    private readonly MapTileCache _cache;
    private readonly Dictionary<(int Zoom, int X, int Y), Bitmap> _images = [];
    private readonly Dictionary<(int Zoom, int X, int Y), int> _pending = [];
    private readonly Dictionary<(int Zoom, int X, int Y), DateTime> _failedUntil = [];
    private CancellationTokenSource _viewportCancellation = new();
    private (double Latitude, double Longitude, int Zoom, double Width, double Height)? _viewport;
    private int _generation;
    private Point? _dragStart, _dragLast;
    private bool _dragged;

    static MapTileControl() => AffectsRender<MapTileControl>(LatitudeProperty, LongitudeProperty, ZoomProperty, MarkersProperty);

    public MapTileControl() : this(new MapTileCache(Path.Combine(AppDataPaths.Root, "map-tiles"), Http)) { }
    public MapTileControl(MapTileCache cache) => _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    public double Latitude { get => GetValue(LatitudeProperty); set => SetValue(LatitudeProperty, value); }
    public double Longitude { get => GetValue(LongitudeProperty); set => SetValue(LongitudeProperty, value); }
    public int Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public IEnumerable<MapMarkerPayload>? Markers { get => GetValue(MarkersProperty); set => SetValue(MarkersProperty, value); }
    public event Action<string>? MarkerSelected;
    public event Action<double, double>? MapPointSelected;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkersProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldCollection) oldCollection.CollectionChanged -= MarkersChanged;
            if (change.NewValue is INotifyCollectionChanged newCollection) newCollection.CollectionChanged += MarkersChanged;
        }
    }

    private void MarkersChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0 || Bounds.Width > 8192 || Bounds.Height > 8192) return;
        context.FillRectangle(Brushes.DimGray, new Rect(Bounds.Size));
        var viewport = (Latitude, Longitude, Zoom, Bounds.Width, Bounds.Height);
        if (_viewport != viewport)
        {
            _viewportCancellation.Cancel(); _viewportCancellation.Dispose();
            _viewportCancellation = new CancellationTokenSource();
            _viewport = viewport; _generation++;
        }
        foreach (var tile in MapViewport.VisibleTiles(Latitude, Longitude, Zoom, Bounds.Width, Bounds.Height))
        {
            var key = (Zoom, tile.X, tile.Y);
            if (_images.TryGetValue(key, out var image)) context.DrawImage(image, new Rect(tile.Left, tile.Top, 256, 256));
            else QueueTile(key, _generation, _viewportCancellation.Token);
        }
        foreach (var marker in Markers ?? [])
        {
            var point = MarkerPoint(marker);
            if (point.X < -20 || point.Y < -20 || point.X > Bounds.Width + 20 || point.Y > Bounds.Height + 20) continue;
            context.DrawEllipse(Brushes.DeepPink, new Pen(Brushes.White, 2), point, 15, 15);
            var label = new FormattedText(marker.Count.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.White);
            context.DrawText(label, new Point(point.X - label.Width / 2, point.Y - label.Height / 2));
        }
        const string attribution = "© OpenStreetMap contributors";
        var credit = new FormattedText(attribution, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, Brushes.White);
        var creditRect = new Rect(Math.Max(0, Bounds.Width - credit.Width - 12), Math.Max(0, Bounds.Height - credit.Height - 6), credit.Width + 8, credit.Height + 4);
        context.FillRectangle(Brushes.Black, creditRect);
        context.DrawText(credit, new Point(creditRect.X + 4, creditRect.Y + 2));
    }

    private Point MarkerPoint(MapMarkerPayload marker)
    {
        var center = MapTileProjection.Project(Latitude, Longitude, Zoom);
        var position = MapTileProjection.Project(marker.Latitude, marker.Longitude, Zoom);
        var world = 1 << Zoom;
        var dx = position.X - center.X;
        if (dx > world / 2d) dx -= world;
        if (dx < -world / 2d) dx += world;
        return new Point(Bounds.Width / 2 + dx * 256, Bounds.Height / 2 + (position.Y - center.Y) * 256);
    }

    private void QueueTile((int Zoom, int X, int Y) key, int generation, CancellationToken token)
    {
        if (_pending.TryGetValue(key, out var existing) && existing == generation) return;
        if (_failedUntil.TryGetValue(key, out var until) && until > DateTime.UtcNow) return;
        _pending[key] = generation;
        _ = FetchAsync(key, generation, token);
    }

    private async Task FetchAsync((int Zoom, int X, int Y) key, int generation, CancellationToken token)
    {
        try
        {
            var data = await _cache.GetTileAsync(key.Zoom, key.X, key.Y, token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || generation != _generation) return;
                using var stream = new MemoryStream(data);
                var bitmap = new Bitmap(stream);
                if (_images.Remove(key, out var old)) old.Dispose();
                _images[key] = bitmap;
                while (_images.Count > 256)
                {
                    var victim = _images.First(); _images.Remove(victim.Key); victim.Value.Dispose();
                }
                InvalidateVisual();
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception) { await Dispatcher.UIThread.InvokeAsync(() => _failedUntil[key] = DateTime.UtcNow.AddSeconds(15)); }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => { if (_pending.TryGetValue(key, out var value) && value == generation) _pending.Remove(key); });
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragStart = _dragLast = e.GetPosition(this); _dragged = false;
        e.Pointer.Capture(this); e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragLast is not { } previous || _dragStart is not { } start) return;
        var current = e.GetPosition(this);
        if (Distance(current, start) > 3) _dragged = true;
        var center = MapTileProjection.Project(Latitude, Longitude, Zoom);
        var next = MapTileProjection.Unproject(center.X - (current.X - previous.X) / 256, center.Y - (current.Y - previous.Y) / 256, Zoom);
        Latitude = next.Latitude; Longitude = next.Longitude; _dragLast = current; e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragStart is null) return;
        var point = e.GetPosition(this);
        if (!_dragged)
        {
            var marker = (Markers ?? []).Select(item => (Item: item, Point: MarkerPoint(item)))
                .FirstOrDefault(item => Distance(item.Point, point) <= 18).Item;
            if (marker is not null) MarkerSelected?.Invoke(marker.Id);
            else
            {
                var center = MapTileProjection.Project(Latitude, Longitude, Zoom);
                var coordinate = MapTileProjection.Unproject(center.X + (point.X - Bounds.Width / 2) / 256,
                    center.Y + (point.Y - Bounds.Height / 2) / 256, Zoom);
                MapPointSelected?.Invoke(coordinate.Latitude, coordinate.Longitude);
            }
        }
        _dragStart = _dragLast = null; e.Pointer.Capture(null); e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y == 0) return;
        Zoom += e.Delta.Y > 0 ? 1 : -1; e.Handled = true;
    }

    private static double Distance(Point first, Point second) => Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _viewportCancellation.Cancel();
        foreach (var image in _images.Values) image.Dispose();
        _images.Clear(); _viewport = null;
        base.OnDetachedFromVisualTree(e);
    }
}
