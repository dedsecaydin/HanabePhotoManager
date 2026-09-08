using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MediaDeviceMonitorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HanabeDeviceTests", Guid.NewGuid().ToString("N"));
    public MediaDeviceMonitorTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("C9400.MP4", true)]
    [InlineData("DSC123.ARW", true)]
    [InlineData("notes.txt", false)]
    public void Discovery_RecognizesNonemptyMediaInCameraSubdirectories(string name, bool expected)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "DCIM", "100MSDCF"));
        File.WriteAllBytes(Path.Combine(folder.FullName, name), [1, 2, 3]);
        MediaDeviceMonitor.ContainsMedia(_root, default).Should().Be(expected);
    }

    [Fact]
    public void Discovery_IgnoresEmptyFilesAndHonorsCancellation()
    {
        File.WriteAllBytes(Path.Combine(_root, "empty.jpg"), []);
        MediaDeviceMonitor.ContainsMedia(_root, default).Should().BeFalse();
        var action = () => MediaDeviceMonitor.ContainsMedia(_root, new CancellationToken(true));
        action.Should().Throw<OperationCanceledException>();
    }
    public void Dispose() => Directory.Delete(_root, true);
}
