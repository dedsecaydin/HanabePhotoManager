using System.IO;
using HanabePhotoManager.Mac.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacCollageColorConverterTests
{
    [Fact]
    public void FailedConversionNeverPublishesAnImageAndRemovesTemporaryFiles()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "icc-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "drivers", "color", "sRGB Color Space Profile.icm");
            using var image = new Image<Rgba32>(4, 3);
            image.Metadata.IccProfile = new IccProfile(File.ReadAllBytes(profile));
            var converter = new MacCollageColorConverter(profile, (_, output, _) => { File.WriteAllText(output, "incomplete"); throw new IOException("conversion failed"); });
            Assert.Throws<InvalidDataException>(() => converter.ConvertToSrgb(image, root));
            Assert.Empty(Directory.GetFiles(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ConvertsTaggedImageUsingIsolatedTemporaryFilesAndCleansUp()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "icc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "drivers", "color", "sRGB Color Space Profile.icm");
            using var image = new Image<Rgba32>(4, 3, new Rgba32(12, 34, 56));
            image.Metadata.IccProfile = new IccProfile(File.ReadAllBytes(profile));
            var converter = new MacCollageColorConverter(profile, (source, output, _) => File.Copy(source, output));
            using var converted = converter.ConvertToSrgb(image, root);
            Assert.Equal(image[0, 0], converted[0, 0]);
            Assert.Empty(Directory.GetFiles(root));
            Assert.NotNull(converter.OutputProfile());
        }
        finally { Directory.Delete(root, true); }
    }
}
