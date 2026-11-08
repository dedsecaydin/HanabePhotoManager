using System.Globalization;
using System.IO;
using System.Text;

namespace HanabePhotoManager.App.Video;

/// <summary>
/// Technical SDR conversion: Sony's published S-Log3 inverse, D65 gamut primaries,
/// and BT.709 OETF. Values outside display gamut are clipped. This is a neutral
/// mathematical conversion, not a creative Sony look or camera calibration.
/// Sources: https://pro.sony/s3/cms-static-content/uploadfile/06/1237494271406.pdf
/// and ITU-R BT.709, https://www.itu.int/rec/R-REC-BT.709/en
/// </summary>
internal static class SonyRec709Transform
{
    // Derived from Sony's published xy primaries at D65 using the normalised-primary matrix.
    private static readonly double[,] Cine =
    {
        { 1.62694741, -0.54013854, -0.08680887 },
        { -0.17851553, 1.41794093, -0.23942540 },
        { -0.04443612, -0.19591997, 1.24035608 }
    };
    private static readonly double[,] SGamut3 =
    {
        { 1.87791513, -0.79416876, -0.08374637 },
        { -0.17680698, 1.35099962, -0.17419264 },
        { -0.02620113, -0.14842226, 1.17462339 }
    };

    internal static double DecodeSLog3(double encoded) => encoded >= 171.2102946929 / 1023.0
        ? Math.Pow(10.0, (encoded * 1023.0 - 420.0) / 261.5) * 0.19 - 0.01
        : (encoded * 1023.0 - 95.0) * 0.01125 / (171.2102946929 - 95.0);

    internal static (double R, double G, double B) Convert(double r, double g, double b, SonySourceGamut gamut)
    {
        var matrix = gamut == SonySourceGamut.SGamut3Cine ? Cine : SGamut3;
        var values = new[] { DecodeSLog3(r), DecodeSLog3(g), DecodeSLog3(b) };
        double channel(int row)
        {
            var linear = Enumerable.Range(0, 3).Sum(column => matrix[row, column] * values[column]);
            linear = Math.Clamp(linear, 0, 1);
            return linear < 0.018 ? 4.5 * linear : 1.099 * Math.Pow(linear, 0.45) - 0.099;
        }
        return (channel(0), channel(1), channel(2));
    }

    internal static void WriteCube(string path, SonySourceGamut gamut, int size = 33)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("TITLE \"Sony S-Log3 to neutral BT.709 SDR\"");
        writer.WriteLine($"LUT_3D_SIZE {size}");
        for (var blue = 0; blue < size; blue++)
        for (var green = 0; green < size; green++)
        for (var red = 0; red < size; red++)
        {
            var (r, g, b) = Convert((double)red / (size - 1), (double)green / (size - 1), (double)blue / (size - 1), gamut);
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{r:F8} {g:F8} {b:F8}"));
        }
    }
}
