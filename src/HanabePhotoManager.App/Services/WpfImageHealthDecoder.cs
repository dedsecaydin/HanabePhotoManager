using System.IO;
using System.Windows.Media.Imaging;

namespace HanabePhotoManager.App.Services;

internal sealed class WpfImageHealthDecoder : IImageHealthDecoder
{
    public ImageHealthDecodeResult Decode(Stream stream)
    {
        try
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            var large = (long)frame.PixelWidth * frame.PixelHeight > 32_000_000;
            if (!large)
            {
                var stride = checked((frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8);
                var pixels = new byte[checked(stride * frame.PixelHeight)];
                frame.CopyPixels(pixels, stride, 0);
            }
            return new(decoder.Frames.Count != 1, large);
        }
        catch (FileFormatException ex) { throw new InvalidDataException(ex.Message, ex); }
    }
}
