using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.Infrastructure.Search;

public sealed class ClipImagePreprocessor
{
    public const int InputSize = 224;
    private static readonly float[] Mean = [0.48145466f, 0.4578275f, 0.40821073f];
    private static readonly float[] StandardDeviation = [0.26862954f, 0.26130258f, 0.27577711f];

    public async Task<float[]> PreprocessAsync(string imagePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        await using var stream = File.OpenRead(imagePath);
        using var image = await Image.LoadAsync<Rgb24>(stream, cancellationToken).ConfigureAwait(false);
        image.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(InputSize, InputSize), Mode = ResizeMode.Crop, Sampler = KnownResamplers.Bicubic }));
        var pixels = new float[3 * InputSize * InputSize];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < InputSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < InputSize; x++)
                {
                    var offset = y * InputSize + x;
                    pixels[offset] = (row[x].R / 255f - Mean[0]) / StandardDeviation[0];
                    pixels[InputSize * InputSize + offset] = (row[x].G / 255f - Mean[1]) / StandardDeviation[1];
                    pixels[2 * InputSize * InputSize + offset] = (row[x].B / 255f - Mean[2]) / StandardDeviation[2];
                }
            }
        });
        return pixels;
    }
}
