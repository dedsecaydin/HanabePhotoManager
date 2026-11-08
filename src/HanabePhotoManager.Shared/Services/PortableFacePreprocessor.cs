using System.IO;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace HanabePhotoManager.App.Services;

/// <summary>Platform-independent RGB tensor and bilinear five-point face alignment.</summary>
internal static class PortableFacePreprocessor
{
    internal static DenseTensor<float> ToDetectorTensor(Image<Rgb24> image) => ToTensor(image, null);
    internal static DenseTensor<float> ToRecognitionTensor(Image<Rgb24> image, FaceRecognitionEngineKind engine) => ToTensor(image, engine);

    private static DenseTensor<float> ToTensor(Image<Rgb24> image, FaceRecognitionEngineKind? engine)
    {
        var tensor = new DenseTensor<float>([1, 3, image.Height, image.Width]);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];
            tensor[0, 0, y, x] = engine is { } kind ? FaceRecognitionMath.PrepareInputValue(pixel.R, kind) : pixel.R;
            tensor[0, 1, y, x] = engine is { } kind2 ? FaceRecognitionMath.PrepareInputValue(pixel.G, kind2) : pixel.G;
            tensor[0, 2, y, x] = engine is { } kind3 ? FaceRecognitionMath.PrepareInputValue(pixel.B, kind3) : pixel.B;
        }
        return tensor;
    }

    internal static Image<Rgb24> Align(Image<Rgb24> source, FaceAlignmentTransform transform)
    {
        var denominator = transform.A * transform.A + transform.B * transform.B;
        if (!double.IsFinite(denominator) || denominator <= 1e-12) throw new InvalidDataException("人脸关键点无法对齐。");
        var output = new Image<Rgb24>(112, 112);
        for (var y = 0; y < output.Height; y++)
        for (var x = 0; x < output.Width; x++)
        {
            var dx = x - transform.OffsetX;
            var dy = y - transform.OffsetY;
            var sx = (transform.A * dx + transform.B * dy) / denominator;
            var sy = (-transform.B * dx + transform.A * dy) / denominator;
            if (!double.IsFinite(sx) || !double.IsFinite(sy) || sx < -1 || sy < -1 || sx >= source.Width || sy >= source.Height) continue;
            var x0 = (int)Math.Floor(sx); var y0 = (int)Math.Floor(sy);
            var fx = sx - x0; var fy = sy - y0;
            var p00 = Pixel(source, x0, y0); var p10 = Pixel(source, x0 + 1, y0);
            var p01 = Pixel(source, x0, y0 + 1); var p11 = Pixel(source, x0 + 1, y0 + 1);
            byte mix(byte a, byte b, byte c, byte d) => (byte)Math.Clamp((int)Math.Round(
                (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy), 0, 255);
            output[x, y] = new Rgb24(mix(p00.R, p10.R, p01.R, p11.R), mix(p00.G, p10.G, p01.G, p11.G), mix(p00.B, p10.B, p01.B, p11.B));
        }
        return output;
    }

    private static Rgb24 Pixel(Image<Rgb24> image, int x, int y) =>
        x >= 0 && y >= 0 && x < image.Width && y < image.Height ? image[x, y] : default;
}
