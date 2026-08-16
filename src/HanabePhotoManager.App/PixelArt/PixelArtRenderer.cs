using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HanabePhotoManager.App.PixelArt;

/// <summary>
/// 图片转像素画的核心渲染：加载源图 → 缩到目标网格 → 最近邻放大导出。
/// </summary>
public static class PixelArtRenderer
{
    /// <summary>加载源图（限制解码宽度以控制内存，OnLoad 立即解码）。</summary>
    public static BitmapSource Load(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 4096;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>计算像素网格尺寸：等比缩放，长边 = targetSize，但绝不放大（原图小于目标时保持原尺寸）。</summary>
    public static (int Width, int Height) CalculateGridSize(int sourceWidth, int sourceHeight, int targetSize)
    {
        var scale = Math.Min(1.0, Math.Min((double)targetSize / sourceWidth, (double)targetSize / sourceHeight));
        return (Math.Max(1, (int)Math.Round(sourceWidth * scale)), Math.Max(1, (int)Math.Round(sourceHeight * scale)));
    }

    /// <summary>计算导出时的块放大倍数：输出长边约 1024px，块至少 1。</summary>
    public static int CalculateBlockSize(int gridWidth, int gridHeight)
    {
        return Math.Max(1, (int)Math.Ceiling(1024d / Math.Max(gridWidth, gridHeight)));
    }

    /// <summary>把源图等比缩到目标网格（长边 = targetSize），返回网格位图与网格尺寸。</summary>
    public static BitmapSource DownscaleToGrid(BitmapSource source, int targetSize, out int width, out int height)
    {
        var (gridWidth, gridHeight) = CalculateGridSize(source.PixelWidth, source.PixelHeight, targetSize);
        width = gridWidth;
        height = gridHeight;
        var scale = (double)gridWidth / source.PixelWidth;

        var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        RenderOptions.SetBitmapScalingMode(transformed, BitmapScalingMode.HighQuality);
        transformed.Freeze();
        return transformed;
    }

    /// <summary>把像素网格按最近邻放大成块状像素画并保存为 PNG（输出长边约 1024px）。</summary>
    public static void Export(BitmapSource grid, string outputPath)
    {
        var block = CalculateBlockSize(grid.PixelWidth, grid.PixelHeight);
        var width = grid.PixelWidth * block;
        var height = grid.PixelHeight * block;

        var drawingVisual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.NearestNeighbor);
        using (var context = drawingVisual.RenderOpen())
        {
            context.DrawImage(grid, new Rect(0, 0, width, height));
        }

        var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(drawingVisual);
        renderTarget.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderTarget));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }
}
