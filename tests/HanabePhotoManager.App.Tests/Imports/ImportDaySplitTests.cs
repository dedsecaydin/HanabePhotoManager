using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests.Imports;

/// <summary>HANABE-IMPORT-DAY-SPLIT：同一天隔了很久自动分成几个日期文件夹，可手动改回一个。</summary>
public sealed class ImportDaySplitTests : IDisposable
{
    private readonly string _root = Path.Combine(@"D:\HanabePhoto\.artifacts\import-day-split-tests", Guid.NewGuid().ToString("N"));
    private static readonly LibraryDate Day = new(2026, 11, 4);

    public ImportDaySplitTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private static DateTime At(int hour, int minute) => new(2026, 11, 4, hour, minute, 0);

    private static IEnumerable<DaySplitMember> Run(string prefix, DateTime start, int count, TimeSpan step) =>
        Enumerable.Range(0, count).Select(i => new DaySplitMember($"{prefix}{i:000}", start + step * i));

    // ---------- 纯逻辑 ----------

    [Fact]
    public void Split_CutsWhereTheGapReachesTheThreshold()
    {
        var members = Run("A", At(9, 12), 20, TimeSpan.FromMinutes(10))
            .Concat(Run("B", At(17, 30), 20, TimeSpan.FromMinutes(10)));

        var segments = ImportDaySplitPlanner.Split(members, TimeSpan.FromHours(2));

        segments.Should().HaveCount(2);
        segments[0].Keys.Should().OnlyContain(key => key.StartsWith('A'));
        segments[1].Keys.Should().OnlyContain(key => key.StartsWith('B'));
        segments[1].GapBefore.Should().Be(At(17, 30) - At(12, 22));
        ImportDaySplitPlanner.Split(members, TimeSpan.FromHours(6)).Should().ContainSingle("间隔不到 6 小时不分段");
    }

    [Fact]
    public void Split_MergesStraySmallRunsIntoTheNearerSegment()
    {
        var members = Run("A", At(9, 0), 10, TimeSpan.FromMinutes(5))
            .Concat(Run("T", At(13, 0), 2, TimeSpan.FromMinutes(1)))   // 试拍 2 张，离下午那段更近
            .Concat(Run("B", At(15, 30), 10, TimeSpan.FromMinutes(5)));

        var segments = ImportDaySplitPlanner.Split(members, TimeSpan.FromHours(2));

        segments.Should().HaveCount(2);
        segments[1].Keys.Should().Contain(["T000", "T001"]);
        segments[1].Start.Should().Be(At(13, 0));
    }

    [Fact]
    public void Split_KeepsPairedFilesTogether()
    {
        var members = Run("A", At(9, 0), 10, TimeSpan.FromMinutes(5)).ToList();
        members.AddRange(Run("B", At(18, 0), 10, TimeSpan.FromMinutes(5)));
        // 同名 RAW 的修改时间晚了一整天的半截（例如被单独复制过），仍跟着 JPG 走。
        members.Add(new DaySplitMember("A000.jpg", At(9, 0), "pair-A000"));
        members.Add(new DaySplitMember("A000.arw", At(19, 0), "pair-A000"));

        var segments = ImportDaySplitPlanner.Split(members, TimeSpan.FromHours(2));

        segments.Should().HaveCount(2);
        segments[0].Keys.Should().Contain(["A000.jpg", "A000.arw"]);
    }

    [Fact]
    public void MergeIntoPrevious_AndRemarksAreUnique()
    {
        var members = Run("A", At(9, 0), 6, TimeSpan.FromMinutes(5))
            .Concat(Run("B", At(13, 0), 6, TimeSpan.FromMinutes(5)))
            .Concat(Run("C", At(19, 0), 6, TimeSpan.FromMinutes(5)));
        var segments = ImportDaySplitPlanner.Split(members, TimeSpan.FromHours(2));
        segments.Should().HaveCount(3);

        var merged = ImportDaySplitPlanner.MergeIntoPrevious(segments, new HashSet<string> { segments[2].AnchorKey });
        merged.Should().HaveCount(2);
        merged[1].Count.Should().Be(12);
        merged[1].Index.Should().Be(1);

        ImportDaySplitPlanner.DefaultRemark(At(9, 0)).Should().Be("上午");
        ImportDaySplitPlanner.DefaultRemark(At(13, 0)).Should().Be("下午");
        ImportDaySplitPlanner.DefaultRemark(At(19, 0)).Should().Be("晚上");
        ImportDaySplitPlanner.UniqueRemarks(["晚上", "晚上", "上午", "晚上"]).Should().Equal("晚上", "晚上2", "上午", "晚上3");
        ImportDaySplitPlanner.FormatGap(TimeSpan.FromMinutes(290)).Should().Be("4 小时 50 分");
    }

    // ---------- 检查页 ----------

    private static ImportPreviewItemViewModel Item(MainWindowViewModel vm, string name, DateTime localTime, int index)
    {
        var file = new SourceMediaFile($@"D:\sample-day-split\{name}.JPG", 1024 * 1024, new DateTimeOffset(localTime));
        return new ImportPreviewItemViewModel(new MediaGroup(name, MediaCategory.Jpeg, file, []),
            MediaCategory.Jpeg, vm.CategoryChoices, false, Day, null, string.Empty, index) { IsSelected = true };
    }

    private MainWindowViewModel ViewModelWithTwoShoots()
    {
        var vm = new MainWindowViewModel { LibraryRoot = _root };
        var index = 0;
        for (var i = 0; i < 8; i++) vm.ImportItems.Add(Item(vm, $"AM{i:000}", At(9, 12).AddMinutes(i * 20), ++index));
        for (var i = 0; i < 6; i++) vm.ImportItems.Add(Item(vm, $"PM{i:000}", At(18, 30).AddMinutes(i * 20), ++index));
        vm.RefreshInlineDatePlan();
        return vm;
    }

    [Fact]
    public void ReviewPage_SplitsADayIntoTwoTargetFolders()
    {
        var vm = ViewModelWithTwoShoots();

        vm.ImportDateFolderDecisions.Should().HaveCount(2);
        vm.ImportShootDayCount.Should().Be(1, "还是一个拍摄日");
        var morning = vm.ImportDateFolderDecisions[0];
        var evening = vm.ImportDateFolderDecisions[1];
        morning.IsSegmented.Should().BeTrue();
        morning.MediaCount.Should().Be(8);
        morning.FinalDirectoryName.Should().Be("11.04_上午");
        evening.FinalDirectoryName.Should().Be("11.04_晚上");
        evening.SegmentGapText.Should().Contain("小时");
        evening.CanMergeWithPrevious.Should().BeTrue();
        vm.ImportTargetFolderCount.Should().Be(2);
        vm.ImportDateCards.Should().HaveCount(2);
        vm.ImportDateCards[1].Items.Should().HaveCount(6).And.OnlyContain(item => item.Name.StartsWith("PM"));
    }

    [Fact]
    public void ReviewPage_ExistingFolderTakesTheFirstSegment()
    {
        Directory.CreateDirectory(Path.Combine(_root, "11月", "11.04_婚礼"));
        var vm = ViewModelWithTwoShoots();

        vm.ImportDateFolderDecisions[0].Strategy.Should().Be(ImportDateFolderStrategy.UseExisting);
        vm.ImportDateFolderDecisions[0].FinalDirectoryName.Should().Be("11.04_婚礼");
        vm.ImportDateFolderDecisions[1].Strategy.Should().Be(ImportDateFolderStrategy.CreateSeparate);
        vm.ImportDateFolderDecisions[1].FinalDirectoryName.Should().Be("11.04_晚上");
        vm.ImportDateFolderDecisions.Should().OnlyContain(decision => decision.IsValid);
    }

    [Fact]
    public void ReviewPage_UserCanKeepTheDayTogetherAndSplitAgain()
    {
        var vm = ViewModelWithTwoShoots();

        vm.KeepImportDayTogetherCommand.Execute(vm.ImportDateFolderDecisions[0]);
        var whole = vm.ImportDateFolderDecisions.Should().ContainSingle().Subject;
        whole.IsDayKeptTogether.Should().BeTrue();
        whole.CanSplitDay.Should().BeTrue();
        whole.MediaCount.Should().Be(14);
        whole.FinalDirectoryName.Should().Be("11.04");

        vm.SplitImportDayCommand.Execute(whole);
        vm.ImportDateFolderDecisions.Should().HaveCount(2);

        vm.MergeImportSegmentWithPreviousCommand.Execute(vm.ImportDateFolderDecisions[1]);
        vm.ImportDateFolderDecisions.Should().ContainSingle().Which.IsDayKeptTogether.Should().BeTrue();
    }

    [Fact]
    public void ReviewPage_RemarksSurviveRebuild_AndSwitchTurnsTheFeatureOff()
    {
        var vm = ViewModelWithTwoShoots();
        vm.ImportDateFolderDecisions[1].Remark = "生日宴";
        vm.ImportItems[0].IsSelected = false;
        vm.RefreshInlineDatePlan();
        vm.ImportDateFolderDecisions[1].Remark.Should().Be("生日宴", "重建方案时按段保留已填的备注");

        vm.AutoSplitImportDays = false;
        var only = vm.ImportDateFolderDecisions.Should().ContainSingle().Subject;
        only.IsSegmented.Should().BeFalse();
        only.HasDaySplitHint.Should().BeFalse("关掉后和以前完全一样");
    }

    [Fact]
    public void MacEngine_UsesTheSameSplitAndOnlyImportsEachSegmentIntoItsFolder()
    {
        var engine = new HanabePhotoManager.Mac.Services.MacImportEngine();
        HanabePhotoManager.Mac.Services.ImportGroupItem Mac(string name, DateTime local) =>
            new(new MediaGroup(name, MediaCategory.Jpeg, new SourceMediaFile($@"D:\sample-mac\{name}.JPG", 10, new DateTimeOffset(local)), []), Day);
        var items = Enumerable.Range(0, 6).Select(i => Mac($"AM{i}", At(9, 0).AddMinutes(i * 10)))
            .Concat(Enumerable.Range(0, 6).Select(i => Mac($"PM{i}", At(19, 0).AddMinutes(i * 10))))
            .ToArray();

        var split = engine.CreateDecisions(_root, items, autoSplit: true, TimeSpan.FromHours(2));
        split.Should().HaveCount(2);
        items.Count(item => HanabePhotoManager.Mac.Services.MacImportEngine.Covers(split[1], item)).Should().Be(6);
        items.Where(item => HanabePhotoManager.Mac.Services.MacImportEngine.Covers(split[1], item))
            .Should().OnlyContain(item => item.Group.GroupKey.StartsWith("PM"));

        engine.CreateDecisions(_root, items).Should().ContainSingle("不开分段时和以前一样");
        engine.CreateDecisions(_root, items, autoSplit: true, TimeSpan.FromHours(2), new HashSet<LibraryDate> { Day })
            .Should().ContainSingle().Which.IsDayKeptTogether.Should().BeTrue();
    }

    [Fact]
    public void ReviewPage_ThresholdChangesTheSplit()
    {
        var vm = ViewModelWithTwoShoots();
        vm.ImportDaySplitHours = 4;
        vm.ImportDateFolderDecisions.Should().HaveCount(2, "间隔约 6 小时 50 分，仍超过 4 小时");
        vm.ImportDaySplitHours = 7;
        vm.ImportDaySplitHours.Should().Be(2, "只接受 1–4 小时");
    }
}
