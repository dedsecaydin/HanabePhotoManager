using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class RetouchedMediaIndexTests : IDisposable
{
    private readonly string _date = Path.Combine(Path.GetTempPath(), $"hanabe-retouched-{Guid.NewGuid():N}", "07.16");

    [Fact]
    public void Build_MatchesRetouchedFilesAcrossSupportedExtensionsAndSuffixes()
    {
        var originals = Directory.CreateDirectory(Path.Combine(_date, "JPG生图")).FullName;
        var retouched = Directory.CreateDirectory(Path.Combine(_date, "修后")).FullName;
        var first = Touch(Path.Combine(originals, "DSC001.JPG"));
        var second = Touch(Path.Combine(originals, "DSC002.jpeg"));
        var firstOutput = Touch(Path.Combine(retouched, "DSC001-修后.PSD"));
        var preferredFirstOutput = Touch(Path.Combine(retouched, "DSC001-修后.png"));
        var secondOutput = Touch(Path.Combine(retouched, "DSC002_FINAL.TIFF"));

        var snapshot = new RetouchedMediaIndex().Build(_date, [first, second]);

        snapshot.RetouchedByOriginal[first].Should().Be(preferredFirstOutput);
        snapshot.RetouchedByOriginal[second].Should().Be(secondOutput);
        snapshot.RetouchedByOriginal[first].Should().NotBe(firstOutput);
    }

    [Fact]
    public void Build_ReturnsStandaloneRetouchedOutputsInsteadOfDroppingThem()
    {
        var retouched = Directory.CreateDirectory(Path.Combine(_date, "修后")).FullName;
        var standalone = Touch(Path.Combine(retouched, "独立成片.webp"));

        var snapshot = new RetouchedMediaIndex().Build(_date, []);

        snapshot.StandaloneRetouchedFiles.Should().ContainSingle().Which.Should().Be(standalone);
    }

    [Fact]
    public void Build_IsCaseInsensitiveAndIgnoresUnsupportedFiles()
    {
        var originals = Directory.CreateDirectory(Path.Combine(_date, "RAW生图")).FullName;
        var retouched = Directory.CreateDirectory(Path.Combine(_date, "修后")).FullName;
        var original = Touch(Path.Combine(originals, "IMAGE01.ARW"));
        var output = Touch(Path.Combine(retouched, "image01.PsB"));
        Touch(Path.Combine(retouched, "image01.txt"));

        var snapshot = new RetouchedMediaIndex().Build(_date, [original]);

        snapshot.RetouchedByOriginal[original].Should().Be(output);
        snapshot.StandaloneRetouchedFiles.Should().BeEmpty();
    }

    private static string Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1]);
        return path;
    }

    public void Dispose()
    {
        var root = Directory.GetParent(_date)?.FullName;
        if (root is not null && Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
