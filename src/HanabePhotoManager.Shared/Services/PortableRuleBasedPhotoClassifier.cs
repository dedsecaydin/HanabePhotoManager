using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.App.Services;

/// <summary>ImageSharp measurements for the shared lightweight classification policy.</summary>
public sealed class PortableRuleBasedPhotoClassifier(Func<string, CancellationToken, Task<bool>>? hasFace = null) : IPhotoClassifier
{
    private readonly Func<string, CancellationToken, Task<bool>> _hasFace = hasFace ?? DetectFaceAsync;
    public string EngineId => "rules";
    public string Version => "rules-1.0";

    public async Task<PhotoClassificationResult> ClassifyAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path)) throw new FileNotFoundException("找不到要分析的照片。", path);
        var measurements = await Task.Run(() => Measure(path, cancellationToken), cancellationToken).ConfigureAwait(false);
        var face = await _hasFace(path, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return RuleBasedClassificationPolicy.Classify(measurements.Luminance, measurements.Green, measurements.Edges, face, path);
    }

    private static async Task<bool> DetectFaceAsync(string path, CancellationToken token)
    {
        try { return (await FaceEmbeddingProvider.Create().DetectAsync(path, token).ConfigureAwait(false)).Count > 0; }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException) { return false; }
    }

    private static (double Luminance, double Green, double Edges) Measure(string path, CancellationToken token)
    {
        Image<Rgb24> loaded;
        try { loaded = Image.Load<Rgb24>(path); }
        catch (UnknownImageFormatException ex) { throw new InvalidDataException("无法解码照片。", ex); }
        catch (InvalidImageContentException ex) { throw new InvalidDataException("无法解码照片。", ex); }
        using var image = loaded;
        if (Math.Max(image.Width, image.Height) > 320)
            image.Mutate(operation => operation.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(320, 320), Sampler = KnownResamplers.Box }));
        var width = image.Width; var height = image.Height;
        var gray = new double[width * height];
        double sum = 0; var greens = 0;
        for (var y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var pixel = image[x, y];
                var luminance = .299 * pixel.R + .587 * pixel.G + .114 * pixel.B;
                gray[y * width + x] = luminance; sum += luminance;
                var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                var delta = max - min;
                if (max < 28 || delta == 0 || delta * 255d / max < 45) continue;
                var hue = max == pixel.R ? 60d * ((pixel.G - pixel.B) / (double)delta % 6) :
                    max == pixel.G ? 60d * ((pixel.B - pixel.R) / (double)delta + 2) : 60d * ((pixel.R - pixel.G) / (double)delta + 4);
                if (hue < 0) hue += 360;
                if (hue >= 64 && hue <= 184) greens++;
            }
        }
        return (sum / gray.Length, greens / (double)gray.Length, EdgeRatio(gray, width, height));
    }

    private static double EdgeRatio(double[] gray, int width, int height)
    {
        if (width < 3 || height < 3) return 0;
        var magnitude = new double[gray.Length]; var direction = new byte[gray.Length];
        for (var y = 1; y < height - 1; y++)
        for (var x = 1; x < width - 1; x++)
        {
            var i = y * width + x;
            var gx = -gray[i-width-1] + gray[i-width+1] - 2*gray[i-1] + 2*gray[i+1] - gray[i+width-1] + gray[i+width+1];
            var gy = -gray[i-width-1] - 2*gray[i-width] - gray[i-width+1] + gray[i+width-1] + 2*gray[i+width] + gray[i+width+1];
            magnitude[i] = Math.Abs(gx) + Math.Abs(gy);
            var angle = Math.Atan2(gy, gx) * 180 / Math.PI;
            if (angle < 0) angle += 180;
            direction[i] = angle < 22.5 || angle >= 157.5 ? (byte)0 : angle < 67.5 ? (byte)1 : angle < 112.5 ? (byte)2 : (byte)3;
        }
        var weak = new bool[gray.Length]; var accepted = new bool[gray.Length]; var queue = new Queue<int>();
        for (var y = 1; y < height - 1; y++)
        for (var x = 1; x < width - 1; x++)
        {
            var i = y * width + x;
            var offset = direction[i] switch { 0 => 1, 1 => width + 1, 2 => width, _ => width - 1 };
            if (magnitude[i] < 70 || magnitude[i] < magnitude[i-offset] || magnitude[i] < magnitude[i+offset]) continue;
            weak[i] = true;
            if (magnitude[i] >= 150) { accepted[i] = true; queue.Enqueue(i); }
        }
        while (queue.TryDequeue(out var i))
        {
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var next = i + dy * width + dx;
                if (next < 0 || next >= gray.Length || !weak[next] || accepted[next]) continue;
                accepted[next] = true; queue.Enqueue(next);
            }
        }
        return accepted.Count(value => value) / (double)gray.Length;
    }
}
