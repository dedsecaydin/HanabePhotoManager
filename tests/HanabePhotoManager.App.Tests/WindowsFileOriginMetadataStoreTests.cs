using FluentAssertions;
using HanabePhotoManager.App.Imports;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class WindowsFileOriginMetadataStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "hanabe-origin-comment-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task WriteAndVerifyAsync_WritesReadableIdentityToJpegComment()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(FindRoot(), "src", "HanabePhotoManager.App", "Assets", "wechat-sponsor-qr.jpg");
        var target = Path.Combine(_directory, "sample.jpg");
        File.Copy(source, target);
        var metadata = new FileOriginMetadata("DSC_7788.JPG", new FileInfo(target).Length, new string('C', 64));
        var store = new WindowsFileOriginMetadataStore();

        var result = await store.WriteAndVerifyAsync(target, metadata, default);

        result.Success.Should().BeTrue(result.Error);
        (await store.ReadAsync(target, default)).Should().Be(metadata);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "HanabePhotoManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
