using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PhotoDetailMetadataReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-metadata-{Guid.NewGuid():N}");

    [Fact]
    public void Read_ReturnsFileIdentityAndFriendlyMissingExifValues()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "sample.jpg");
        File.WriteAllBytes(path, [1, 2, 3]);

        var metadata = new PhotoDetailMetadataReader().Read(path);

        metadata.Name.Should().Be("sample");
        metadata.Extension.Should().Be("JPG");
        metadata.FileSize.Should().NotBeNullOrWhiteSpace();
        metadata.Iso.Should().Be("未记录");
        metadata.Aperture.Should().Be("未记录");
        metadata.Shutter.Should().Be("未记录");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
