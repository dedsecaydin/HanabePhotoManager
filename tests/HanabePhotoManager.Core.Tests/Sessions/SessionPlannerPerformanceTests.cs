using System.Diagnostics;
using System.Text.Json;
using HanabePhotoManager.Core.Sessions;
using Xunit.Abstractions;
namespace HanabePhotoManager.Core.Tests.Sessions;
public sealed class SessionPlannerPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void HundredThousandSeparatedMembersRemainLinearAfterSorting()
    {
        const int count = 100_000;
        var start = new DateTime(2020, 1, 1);
        var members = Enumerable.Range(0, count).Select(index => new CaptureMember(
            index.ToString("D6"), index.ToString("D6") + ".jpg", start.AddMinutes(index * 31d), null, "SameCamera")).ToArray();
        var timer = Stopwatch.StartNew();
        var result = SessionPlanner.Detect(members);
        timer.Stop();
        output.WriteLine($"100000 members / separated sessions: {timer.Elapsed.TotalMilliseconds:F1} ms");
        var directory = @"D:\HanabePhoto\.artifacts\sessions-tests-results";
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "planner-100k-benchmark.json"), JsonSerializer.Serialize(new
        { Members = count, Groups = result.Groups.Count, ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds, SyntheticFixture = true }));
        Assert.Equal(count, result.Groups.Count);
        Assert.All(result.Groups, group => { Assert.Single(group.Members); Assert.False(group.NeedsReview); });
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(20), $"Unexpected repeated full-input scans: {timer.Elapsed}.");
    }

    [Fact]
    public void OverflowingCorrectionReportsTheMemberWithoutReturningPartialGroups()
    {
        var member = new CaptureMember("old", "old.jpg", DateTime.MinValue, null, "A");
        var error = Assert.Throws<ArgumentException>(() => SessionPlanner.Detect([member], corrections: new Dictionary<string, double> { ["A"] = -1 }));
        Assert.Contains("old.jpg", error.Message);
        Assert.Contains("有效日期范围", error.Message);
    }
}
