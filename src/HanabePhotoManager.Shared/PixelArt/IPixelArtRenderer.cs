namespace HanabePhotoManager.App.PixelArt;

public interface IPixelArtRenderer
{
    object Load(string path);
    object DownscaleToGrid(object source, int targetSize, out int width, out int height);
    void Export(object grid, string outputPath);
}

internal sealed class UnavailablePixelArtRenderer : IPixelArtRenderer
{
    public object Load(string path) => throw new PlatformNotSupportedException("此平台尚未配置像素画渲染。");
    public object DownscaleToGrid(object source, int targetSize, out int width, out int height) => throw new PlatformNotSupportedException("此平台尚未配置像素画渲染。");
    public void Export(object grid, string outputPath) => throw new PlatformNotSupportedException("此平台尚未配置像素画渲染。");
}
