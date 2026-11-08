using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace HanabePhotoManager.App.Services;

/// <summary>Read-only ImageSharp health check for platforms without WIC.</summary>
public sealed class PortableImageHealthDecoder : IImageHealthDecoder
{
    public ImageHealthDecodeResult Decode(Stream stream)
    {
        try
        {
            var origin = stream.Position;
            var info = Image.Identify(stream);
            var large = (long)info.Width * info.Height > 32_000_000;
            var multiple = info.FrameMetadataCollection.Count > 1;
            if (!large)
            {
                stream.Position = origin;
                using var image = Image.Load(new DecoderOptions { MaxFrames = 1 }, stream);
            }
            return new ImageHealthDecodeResult(multiple, large);
        }
        catch (UnknownImageFormatException ex) { throw new InvalidDataException(ex.Message, ex); }
        catch (InvalidImageContentException ex) { throw new InvalidDataException(ex.Message, ex); }
    }
}
