using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.Core.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportDateFolderPreflightServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HanabePhotoManagerTests", Guid.NewGuid().ToString("N"));

    public ImportDateFolderPreflightServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void CreateDecisions_NoExistingFolder_UsesSanitizedRemarkPreview()
    {
        var decision = new ImportDateFolderPreflightService().CreateDecisions(
            _root, [(new LibraryDate(2026, 8, 19), 12)]).Single();

        decision.Remark = "08.19_客户:寿司";

        decision.Strategy.Should().Be(ImportDateFolderStrategy.CreateSeparate);
        decision.IsValid.Should().BeTrue();
        decision.FinalDirectoryName.Should().Be("08.19_客户_寿司");
        decision.FinalDirectoryPath.Should().Be(Path.Combine(_root, "8月", "08.19_客户_寿司"));
    }

    [Fact]
    public void ExistingFolder_RequiresAnExplicitStrategyAndCandidate()
    {
        Directory.CreateDirectory(Path.Combine(_root, "8月", "08.19_旧备注"));
        var decision = new ImportDateFolderPreflightService().CreateDecisions(
            _root, [(new LibraryDate(2026, 8, 19), 3)]).Single();

        decision.HasExistingFolders.Should().BeTrue();
        decision.IsValid.Should().BeFalse();
        decision.ValidationMessage.Should().Contain("处理方式");

        decision.SelectedExistingFolder.Should().Be(decision.ExistingFolders.Single());
        decision.Strategy = ImportDateFolderStrategy.RenameExisting;
        decision.Remark.Should().Be("旧备注");
        decision.Strategy = ImportDateFolderStrategy.UseExisting;

        decision.IsValid.Should().BeTrue();
        decision.FinalDirectoryName.Should().Be("08.19_旧备注");
    }

    [Fact]
    public void CreateSeparate_RejectsBlankRemarkWhenDateAlreadyExists()
    {
        Directory.CreateDirectory(Path.Combine(_root, "8月", "08.19_旧备注"));
        var decision = new ImportDateFolderPreflightService().CreateDecisions(
            _root, [(new LibraryDate(2026, 8, 19), 1)]).Single();

        decision.Strategy = ImportDateFolderStrategy.CreateSeparate;

        decision.IsValid.Should().BeFalse();
        decision.ValidationMessage.Should().Contain("不同备注");
    }

    [Fact]
    public void RenameExisting_RenamesTheSelectedFolderBeforeImport()
    {
        var oldPath = Directory.CreateDirectory(Path.Combine(_root, "8月", "08.19_旧备注")).FullName;
        var service = new ImportDateFolderPreflightService();
        var decision = service.CreateDecisions(_root, [(new LibraryDate(2026, 8, 19), 1)]).Single();
        decision.Strategy = ImportDateFolderStrategy.RenameExisting;
        decision.SelectedExistingFolder = decision.ExistingFolders.Single();
        decision.Remark = "新备注";

        service.ApplyRequiredRenames([decision]).Should().BeTrue();

        Directory.Exists(oldPath).Should().BeFalse();
        Directory.Exists(Path.Combine(_root, "8月", "08.19_新备注")).Should().BeTrue();
        decision.FinalDirectoryPath.Should().Be(Path.Combine(_root, "8月", "08.19_新备注"));
    }

    [Fact]
    public void MultipleExistingFolders_UsesTheExplicitlySelectedFolder()
    {
        Directory.CreateDirectory(Path.Combine(_root, "8月", "08.19_A"));
        Directory.CreateDirectory(Path.Combine(_root, "8月", "08.19_B"));
        var decision = new ImportDateFolderPreflightService().CreateDecisions(
            _root, [(new LibraryDate(2026, 8, 19), 1)]).Single();
        decision.Strategy = ImportDateFolderStrategy.UseExisting;
        decision.SelectedExistingFolder = decision.ExistingFolders.Single(folder => folder.Name.EndsWith("_B"));

        decision.FinalDirectoryName.Should().Be("08.19_B");
        decision.IsValid.Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
