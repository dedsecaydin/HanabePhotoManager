using System.IO;
using System.Reflection;
using FluentAssertions;
using HanabePhotoManager.App.Browsing;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests.Browsing;

/// <summary>已导入日期的同日分场：虚拟分组、同日父子级、按场次拆分的移动清单。</summary>
public class LibraryDaySessionTests
{
    private const string Day = @"D:\lib\11月\11.07";
    private static readonly DateTime Morning = new(2026, 11, 7, 9, 0, 0);
    private static readonly DateTime Evening = new(2026, 11, 7, 19, 0, 0);

    private static IEnumerable<(string, string, DateTime)> TwoShoots()
    {
        for (var i = 0; i < 6; i++)
        {
            yield return ($@"{Day}\RAW直出\JK{i:0000}（DSC{i:0000}）.ARW", "RAW直出", Morning.AddMinutes(i));
            yield return ($@"{Day}\JPG直出\JK{i:0000}（DSC{i:0000}）.JPG", "JPG直出", Morning.AddMinutes(i));
        }
        for (var i = 10; i < 16; i++)
            yield return ($@"{Day}\JPG直出\JK{i:0000}（DSC{i:0000}）.JPG", "JPG直出", Evening.AddMinutes(i));
        yield return ($@"{Day}\视频\JK0020（C3858）.MP4", "视频", Evening.AddMinutes(30));
        yield return ($@"{Day}\JPG直出\JK0020（C3858T01）.JPG", "JPG直出", Evening.AddMinutes(30));
        // 修后：修图时间不参与分场，按同名原片归到第 2 场
        yield return ($@"{Day}\修后\JK0012（DSC0012）.JPG", "修后", new DateTime(2026, 11, 8, 23, 0, 0));
    }

    [Fact]
    public void Plan_SplitsByGap_KeepsPairsTogether_AndRetouchedFollowsOriginal()
    {
        var sessions = LibraryDaySessionPlanner.Plan(TwoShoots(), TimeSpan.FromHours(2));

        sessions.Should().HaveCount(2);
        sessions[0].Remark.Should().Be("上午");
        sessions[1].Remark.Should().Be("晚上");
        sessions[0].Count.Should().Be(12);
        sessions[1].Paths.Should().Contain(path => path.EndsWith("（C3858）.MP4"))
            .And.Contain(path => path.EndsWith("（C3858T01）.JPG"))
            .And.Contain(path => path.Contains(@"\修后\"));
        sessions[1].GapText.Should().StartWith("距上一场 ");
        sessions[0].TimeText.Should().Be("09:00–09:05");
    }

    [Fact]
    public void Plan_IgnoresFilesWithoutCaptureTime_AndNeverSplitsWhenNoneKnown()
    {
        // 导入后修改时间是导入时刻：读不到拍摄时间的文件不能参与分场
        var items = TwoShoots().Select(item => (item.Item1, item.Item2, (DateTime?)item.Item3))
            .Append(($@"{Day}\JPG直出\JK0099（DSC0099）.JPG", "JPG直出", (DateTime?)null))
            .ToArray();
        var sessions = LibraryDaySessionPlanner.Plan(items, TimeSpan.FromHours(2));
        sessions.Should().HaveCount(2);
        sessions[0].Paths.Should().Contain(path => path.Contains("JK0099"));

        LibraryDaySessionPlanner.Plan(items.Select(item => (item.Item1, item.Item2, (DateTime?)null)), TimeSpan.FromHours(2))
            .Should().HaveCount(1);
    }

    [Fact]
    public void SplitPreview_ReplansWhenThresholdSliderMoves_AndKeepsEditedRemark()
    {
        var calls = new List<double>();
        DaySplitPreviewPlan Replan(double hours)
        {
            calls.Add(hours);
            var sessions = LibraryDaySessionPlanner.Plan(TwoShoots(), TimeSpan.FromHours(hours));
            return new DaySplitPreviewPlan(
                sessions.Select((session, i) => new DaySplitPreviewSession(i, session.Remark, session.TimeText, session.Count, session.GapText, "11.07", i == 0 ? "11.07" : null)).ToArray(),
                sessions.Skip(1).Sum(session => session.Count));
        }

        var preview = new DaySplitPreview("11.07", 2, Replan);
        preview.Sessions.Should().HaveCount(2);
        preview.CanSplit.Should().BeTrue();
        preview.Sessions[1].Remark = "灵妙之音";
        preview.ThresholdHours = 6;
        preview.Sessions[1].Remark.Should().Be("灵妙之音");
        preview.ThresholdText.Should().Be("6 小时");
        preview.ThresholdHours = 12;  // 拖到不分场的间隔（测试里直接给值）
        preview.Sessions.Should().HaveCount(1);
        preview.CanSplit.Should().BeFalse();
        preview.Validate().Should().NotBeNull();
        DaySplitPreview.FormatHours(0.5).Should().Be("30 分钟");
        DaySplitPreview.FormatHours(1.5).Should().Be("1.5 小时");
        calls.Should().Equal(2, 6, 12);
    }

    [Fact]
    public void Plan_SingleShoot_ReturnsOneSession()
    {
        LibraryDaySessionPlanner.Plan(TwoShoots(), TimeSpan.FromHours(12)).Should().HaveCount(1);
    }

    [Theory]
    [InlineData(@"x\JK0020（C3858）.MP4", "JK0020（C3858）")]
    [InlineData(@"x\JK0020（C3858T01）.JPG", "JK0020（C3858）")]
    [InlineData(@"x\JK0020（C3858M01）.XML", "JK0020（C3858）")]
    [InlineData(@"x\DSC0001.ARW.xmp", "DSC0001")]
    [InlineData(@"x\C0001T01.JPG", "C0001")]
    public void StemKey_MatchesAllFilesOfOneShot(string path, string expected)
    {
        LibraryDaySessionPlanner.StemKey(path).Should().Be(expected.ToUpperInvariant());
    }

    [Fact]
    public void SplitPlan_MovesLaterSessions_LeavesRetouchedAndFirstSession()
    {
        var sessions = LibraryDaySessionPlanner.Plan(TwoShoots(), TimeSpan.FromHours(2));
        var files = TwoShoots().Select(item => item.Item1)
            .Append($@"{Day}\视频\JK0020（C3858M01）.XML")
            .Append($@"{Day}\RAW直出\JK0012（DSC0012）.ARW.xmp")
            .ToArray();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"D:\lib\11月\11.07_晚上" };

        var plan = LibraryDaySessionPlanner.BuildSplitPlan(Day, sessions, files, existing.Contains);

        plan.TargetFolders.Should().Equal(Day, @"D:\lib\11月\11.07_晚上2");
        plan.Moves.Should().NotContain(move => move.Source.Contains(@"\修后\"));
        plan.Moves.Should().Contain(move => move.Source.EndsWith("（C3858M01）.XML") && move.Target == @"D:\lib\11月\11.07_晚上2\视频\JK0020（C3858M01）.XML");
        plan.Moves.Should().Contain(move => move.Source.EndsWith("JK0012（DSC0012）.ARW.xmp"));
        plan.Moves.Should().NotContain(move => move.Source.Contains("JK0001"));
        plan.Moves.Should().HaveCount(6 + 2 + 2);
        plan.FilesPerFolder.Should().Equal(12, 10);
    }

    [Fact]
    public void DateTree_GroupsSameDayFoldersUnderOneParent()
    {
        var root = Path.Combine(Path.GetTempPath(), "HanabeDayGroup", Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var name in new[] { "11.07", "11.07_晚上", "11.08" })
                Directory.CreateDirectory(Path.Combine(root, "11月", name));
            var discover = typeof(MainWindowViewModel).GetMethod("DiscoverDates", BindingFlags.NonPublic | BindingFlags.Static)!;
            var years = (IReadOnlyList<LibraryDateNode>)discover.Invoke(null, [root])!;
            var month = years.Single().Children.Single();

            month.Children.Should().HaveCount(2);
            var group = month.Children[0];
            group.IsDayGroup.Should().BeTrue();
            group.IsSelectable.Should().BeTrue();
            group.Children.Select(child => child.ShortTitle).Should().Equal("11.07", "11.07_晚上");
            group.FolderPaths.Should().HaveCount(2);
            group.SelectionKey.Should().StartWith("day:").And.EndWith("-11-07");
            month.Children[1].IsDayGroup.Should().BeFalse();

            var flatten = typeof(MainWindowViewModel).GetMethod("FlattenDateNodes", BindingFlags.NonPublic | BindingFlags.Static)!;
            ((IReadOnlyList<LibraryDateNode>)flatten.Invoke(null, [years])!).Should().HaveCount(3, "父级不重复计入日期数");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
