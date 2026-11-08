using System.IO;
using System.Reflection;
using FluentAssertions;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>导入页三步：选择来源 → 检查确认（日期方案随队列自动更新，保留用户选择）→ 完成 → 重新开始。</summary>
public sealed class ImportStagesTests : IDisposable
{
    private readonly string _root = Path.Combine(@"D:\HanabePhoto\.artifacts\import-stage-tests", Guid.NewGuid().ToString("N"));

    public ImportStagesTests() => Directory.CreateDirectory(_root);

    private static ImportPreviewItemViewModel Item(MainWindowViewModel vm, LibraryDate date, int index)
    {
        var file = new SourceMediaFile($@"D:\sample\C{index:0000}.MP4", 1024 * 1024, DateTimeOffset.UtcNow);
        return new ImportPreviewItemViewModel(new MediaGroup($"C{index:0000}", MediaCategory.Video, file, []),
            MediaCategory.Video, vm.CategoryChoices, false, date, null, string.Empty, index) { IsSelected = true };
    }

    [Fact]
    public void Stages_FollowTheQueueAndResults()
    {
        var vm = new MainWindowViewModel { LibraryRoot = _root };
        vm.ImportStage.Should().Be("Source");
        vm.ImportWizardSteps.Should().HaveCount(3);

        vm.ImportItems.Add(Item(vm, new LibraryDate(2026, 10, 1), 1));
        vm.ImportStage.Should().Be("Review");
        vm.ImportWizardStep.Should().Be(2);
        vm.CanGoToImportWizardStep(1).Should().BeTrue("检查页可以回到选择来源");

        typeof(MainWindowViewModel).GetMethod("SetImportSummary", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(vm, [1, 0, 0]);
        vm.ImportStage.Should().Be("Done");
        vm.ImportWizardSteps.Should().OnlyContain(step => step.State == "done");

        vm.StartNewImportCommand.Execute(null);
        vm.ImportStage.Should().Be("Source");
        vm.ImportItems.Should().BeEmpty();
        vm.ImportSuccessCount.Should().Be(0);
    }

    [Fact]
    public void InlinePlan_RebuildsAfterQueueEdits_AndKeepsTypedRemark()
    {
        var vm = new MainWindowViewModel { LibraryRoot = _root };
        var day1 = new LibraryDate(2026, 10, 1);
        var day2 = new LibraryDate(2026, 10, 2);
        var a = Item(vm, day1, 1);
        var b = Item(vm, day1, 2);
        var c = Item(vm, day2, 3);
        foreach (var item in new[] { a, b, c }) vm.ImportItems.Add(item);

        vm.RefreshInlineDatePlan();
        vm.IsImportDateFolderPreflightVisible.Should().BeTrue();
        vm.ImportDateCards.Should().HaveCount(2);
        vm.ImportDateFolderDecisions.Single(d => d.Date == day1).Remark = "婚礼";

        b.IsSelected = false;
        vm.RefreshInlineDatePlan();

        var decision = vm.ImportDateFolderDecisions.Single(d => d.Date == day1);
        decision.MediaCount.Should().Be(1);
        decision.Remark.Should().Be("婚礼", "重建方案时保留每个日期已填的备注");
        decision.FinalDirectoryName.Should().Contain("婚礼");
        vm.CanEditImportOrganization.Should().BeTrue("有方案时也能继续调整队列");

        var card = vm.ImportDateCards.Single(card => card.Decision.Date == day1);
        card.Breakdown.Should().ContainSingle().Which.Count.Should().Be(1);
        card.Strip.Should().ContainSingle();
    }

    [Fact]
    public void InlinePlan_UnresolvedItemsBlockThePlanWithAClearHint()
    {
        var vm = new MainWindowViewModel { LibraryRoot = _root };
        var file = new SourceMediaFile(@"D:\sample\X0001.JPG", 10, DateTimeOffset.UtcNow);
        var unresolved = new ImportPreviewItemViewModel(new MediaGroup("X0001", MediaCategory.Unconfirmed, file, []),
            MediaCategory.Unconfirmed, vm.CategoryChoices, false, null, null, string.Empty, 1) { IsSelected = true };
        vm.ImportItems.Add(unresolved);

        vm.RefreshInlineDatePlan();

        vm.IsImportDateFolderPreflightVisible.Should().BeFalse();
        vm.HasImportUnresolved.Should().BeTrue();
        vm.ImportActionHint.Should().Contain("读不到日期");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }
}
