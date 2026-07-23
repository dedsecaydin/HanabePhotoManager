using FluentAssertions;
using HanabePhotoManager.Core.Performance;

namespace HanabePhotoManager.Core.Tests.Performance;

public sealed class ThrottledProgressTests
{
    [Fact]
    public void Report_ForwardsFirstUpdateAndLimitsRapidUpdates()
    {
        var now = DateTimeOffset.Parse("2026-07-15T00:00:00Z");
        var received = new List<int>();
        var progress = new ThrottledProgress<int>(
            received.Add,
            TimeSpan.FromMilliseconds(100),
            () => now);

        progress.Report(1);
        now = now.AddMilliseconds(20);
        progress.Report(2);
        now = now.AddMilliseconds(80);
        progress.Report(3);

        received.Should().Equal(1, 3);
    }

    [Fact]
    public void Report_UsesLatestAllowedUpdateAfterInterval()
    {
        var now = DateTimeOffset.Parse("2026-07-15T00:00:00Z");
        var received = new List<string>();
        var progress = new ThrottledProgress<string>(
            received.Add,
            TimeSpan.FromMilliseconds(120),
            () => now);

        progress.Report("first");
        now = now.AddMilliseconds(119);
        progress.Report("suppressed");
        now = now.AddMilliseconds(1);
        progress.Report("next");

        received.Should().Equal("first", "next");
    }
}
