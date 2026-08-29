using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportResumeTargetResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HanabePhotoManagerTests", Guid.NewGuid().ToString("N"));
    public ImportResumeTargetResolverTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Resolve_UsesPersistedTargetWhenItExists()
    {
        var target = Directory.CreateDirectory(Path.Combine(_root, "8月", "08.19_客户")).FullName;
        var result = ImportResumeTargetResolver.Resolve(_root, Entry(target));
        result.Success.Should().BeTrue();
        result.TargetDirectory.Should().Be(target);
    }

    [Fact]
    public void Resolve_LegacyEntryUsesTheOnlySameDateFolder()
    {
        var target = Directory.CreateDirectory(Path.Combine(_root, "8月", "08.19_旧备注")).FullName;
        var result = ImportResumeTargetResolver.Resolve(_root, Entry(""));
        result.Success.Should().BeTrue();
        result.TargetDirectory.Should().Be(target);
    }

    [Fact]
    public void Resolve_LegacyEntryRejectsAmbiguousSameDateFolders()
    {
        Directory.CreateDirectory(Path.Combine(_root, "8月", "08.19_A"));
        Directory.CreateDirectory(Path.Combine(_root, "8月", "08.19_B"));
        var result = ImportResumeTargetResolver.Resolve(_root, Entry(""));
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("多个");
    }

    private static ImportResumeEntry Entry(string target) => new()
    {
        Year = 2026, Month = 8, Day = 19, TargetDateDirectory = target,
    };

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
