using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.Mac.Services;

namespace HanabePhotoManager.Mac.ViewModels;

/// <summary>一张缩略图：创建后在后台加载，加载不了（例如 Windows 上的 RAW）就显示扩展名徽标。</summary>
public sealed partial class ThumbViewModel : ObservableObject
{
    [ObservableProperty] private Bitmap? _image;

    public ThumbViewModel(string path, int size = 240)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        Badge = System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        _ = LoadAsync(size);
    }

    public string Path { get; }
    public string Name { get; }
    public string Badge { get; }

    private async Task LoadAsync(int size)
    {
        var bitmap = await ThumbnailService.LoadAsync(Path, size).ConfigureAwait(false);
        if (bitmap is not null)
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => Image = bitmap);
    }
}

/// <summary>分类份额（比例条 + 图例）。</summary>
public sealed record CategoryShare(string Name, int Count, double BarWidth, IBrush Brush)
{
    public string Label => $"{Name} {Count:N0}";

    public static IBrush BrushFor(string category) => new SolidColorBrush(category switch
    {
        var c when c.Contains("RAW") => Color.FromRgb(0xB9, 0xA2, 0xFF),
        var c when c.Contains("JPG") => Color.FromRgb(0xFF, 0x8F, 0xB8),
        var c when c.Contains("action") => Color.FromRgb(0x6F, 0xB7, 0xFF),
        var c when c.Contains("视频") => Color.FromRgb(0xFF, 0xB3, 0x6B),
        var c when c.Contains("修后") => Color.FromRgb(0x7E, 0xE0, 0xC3),
        var c when c.Contains("素材") => Color.FromRgb(0xC9, 0xC9, 0xD6),
        _ => Color.FromRgb(0x9A, 0x96, 0xA8),
    });
}
