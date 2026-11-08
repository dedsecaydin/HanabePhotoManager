namespace HanabePhotoManager.App.Services;

public sealed record ImageHealthDecodeResult(bool HasMultipleFrames, bool ExceedsPixelLimit);
public interface IImageHealthDecoder
{
    ImageHealthDecodeResult Decode(Stream stream);
}

internal sealed class UnavailableImageHealthDecoder : IImageHealthDecoder
{
    public ImageHealthDecodeResult Decode(Stream stream) => throw new NotSupportedException("此平台尚未配置图像健康检查解码器。");
}
