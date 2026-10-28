using HanabePhotoManager.Core.Sessions;
using HanabePhotoManager.Infrastructure.Sessions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
namespace HanabePhotoManager.Infrastructure.Tests.Sessions;
public class ExifCaptureTimeReaderTests
{
    [Fact]
    public void ReadsOriginalTimeAndOffsetWithoutUsingComputerTimezone()
    {
        var dir = Path.Combine(@"D:\HanabePhoto\.artifacts\sessions-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { var path = Path.Combine(dir, "fixture.jpg"); using (var image = new Image<Rgb24>(8, 8)) { image.Metadata.ExifProfile = new ExifProfile(); image.Metadata.ExifProfile.SetValue(ExifTag.DateTimeOriginal, "2026:01:02 03:04:05"); image.Metadata.ExifProfile.SetValue(ExifTag.OffsetTimeOriginal, "+08:00"); image.Metadata.ExifProfile.SetValue(ExifTag.SubsecTimeOriginal, "123"); image.SaveAsJpeg(path); } var before = File.ReadAllBytes(path); var read = new ExifCaptureTimeReader().Read(path); Assert.Null(read.Issue); Assert.Equal(TimeSpan.FromHours(8), read.UtcOffset); Assert.Equal(DateTimeKind.Unspecified, read.LocalTime!.Value.Kind); Assert.Equal(123, read.LocalTime.Value.Millisecond); Assert.Equal(before, File.ReadAllBytes(path)); }
        finally { Directory.Delete(dir, true); }
    }
    [Fact]
    public void MissingMetadataDoesNotFallBackToFilesystemDate()
    {
        var dir = Path.Combine(@"D:\HanabePhoto\.artifacts\sessions-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { var path = Path.Combine(dir, "fixture.jpg"); using (var image = new Image<Rgb24>(8, 8)) image.SaveAsJpeg(path); var read = new ExifCaptureTimeReader().Read(path); Assert.Null(read.LocalTime); Assert.Null(read.UtcOffset); Assert.NotNull(read.Issue); }
        finally { Directory.Delete(dir, true); }
    }
}
