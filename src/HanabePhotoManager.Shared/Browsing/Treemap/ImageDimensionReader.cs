namespace HanabePhotoManager.App.Browsing.Treemap;

/// <summary>
/// Reads image dimensions from file headers without full decode.
/// Returns (width, height) or null if dimensions cannot be determined.
/// </summary>
internal static class ImageDimensionReader
{
    private static readonly HashSet<string> JpegExtensions = new(
        [".jpg", ".jpeg", ".jpe"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> PngExtensions = new(
        [".png"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Quickly read pixel dimensions from a supported image file header.
    /// Returns (width, height) or null for unsupported formats.
    /// </summary>
    public static (int width, int height)? ReadDimensions(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
        {
            return null;
        }

        var ext = System.IO.Path.GetExtension(filePath);
        try
        {
            if (JpegExtensions.Contains(ext))
                return ReadJpegDimensions(filePath);
            if (PngExtensions.Contains(ext))
                return ReadPngDimensions(filePath);
        }
        catch
        {
            // Silently fail — caller falls back to default
        }

        return null;
    }

    private static (int width, int height)? ReadJpegDimensions(string path)
    {
        using var stream = new System.IO.FileStream(
            path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
        // Read SOF marker to get dimensions
        stream.Seek(2, System.IO.SeekOrigin.Begin); // skip SOI
        var buf = new byte[4];
        while (stream.Position < stream.Length - 1)
        {
            if (stream.ReadByte() != 0xFF) continue;
            var marker = (byte)stream.ReadByte();
            if (marker == 0x01) continue; // TEM
            if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB)
            {
                // SOF marker found
                stream.ReadExactly(buf, 0, 4);
                var length = (buf[0] << 8) | buf[1];
                stream.ReadExactly(buf, 0, length - 2);
                if (length >= 7)
                {
                    var height = (buf[1] << 8) | buf[2];
                    var width = (buf[3] << 8) | buf[4];
                    if (width > 0 && height > 0)
                        return (width, height);
                }

                break;
            }

            // Skip this marker
            if (stream.Read(buf, 0, 2) < 2) break;
            var skip = (buf[0] << 8) | buf[1];
            stream.Seek(skip - 2, System.IO.SeekOrigin.Current);
        }

        return null;
    }

    private static (int width, int height)? ReadPngDimensions(string path)
    {
        using var stream = new System.IO.FileStream(
            path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
        stream.Seek(16, System.IO.SeekOrigin.Begin); // skip 8-byte sig + 4-byte len + IHDR
        var buf = new byte[8];
        if (stream.Read(buf, 0, 8) < 8) return null;
        var width = (buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3];
        var height = (buf[4] << 24) | (buf[5] << 16) | (buf[6] << 8) | buf[7];
        if (width > 0 && height > 0)
            return (width, height);
        return null;
    }
}