using HanabePhotoManager.Core.Sessions;
namespace HanabePhotoManager.Core.Tests.Sessions;
public class SessionPlannerTests
{
    private static CaptureMember M(string id, int minute, string camera = "A", TimeSpan? offset = null) => new(id, id + ".jpg", new DateTime(2026, 1, 1).AddMinutes(minute), offset, camera);
    [Fact] public void StrictThresholdAndStableOrder() { var p = SessionPlanner.Detect([M("c", 61), M("b", 30), M("a", 0)]); Assert.Equal(2, p.Groups.Count); Assert.Equal(new[] { "a", "b" }, p.Groups[0].Members.Select(x => x.Id)); }
    [Fact] public void MissingAndUnrelatedCameraClocksNeedReview() { var p = SessionPlanner.Detect([M("a", 0), M("b", 1, "B"), new("x", "x", null, null, "A")]); Assert.Equal(3, p.Groups.Count); Assert.Contains(p.Groups, g => g.NeedsReview); }
    [Fact] public void OffsetTimesNormalizeAndManualBoundariesAreReversible() { var p = SessionPlanner.Detect([M("a", 60, "A", TimeSpan.FromHours(1)), M("b", 1, "B", TimeSpan.Zero)]); Assert.Single(p.Groups); var split = SessionPlanner.Split(p.Groups, 0, 1); Assert.Equal(2, split.Count); Assert.Single(SessionPlanner.Merge(split, 0)); }
    [Fact]
    public void StableIdsAndCorrectionsAffectEffectiveRange()
    {
        var input = new[] { M("b", 60, "B", TimeSpan.FromHours(1)), M("a", 0, "A", TimeSpan.Zero) }; var first = SessionPlanner.Detect(input); var second = SessionPlanner.Detect(input.Reverse()); Assert.Equal(first.Groups[0].Id, second.Groups[0].Id); Assert.Equal(first.Groups[0].Start, first.Groups[0].End); var corrected = SessionPlanner.Detect([M("a", 0, "A"), M("b", 60, "B")], corrections: new Dictionary<string, double> { { "A", 0 }, { "B", -60 } }); Assert.Single(corrected.Groups); Assert.Equal(corrected.Groups[0].Start, corrected.Groups[0].End);
    }
    [Theory][InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(10000000)] public void InvalidCorrectionRejected(double correction) => Assert.Throws<ArgumentOutOfRangeException>(() => SessionPlanner.Detect([M("a", 0)], corrections: new Dictionary<string, double> { { "A", correction } }));
    [Fact] public void CrossMidnightAndEmptyAndDuplicateMembers() { Assert.Empty(SessionPlanner.Detect([]).Groups); Assert.Single(SessionPlanner.Detect([M("a", 1430), M("b", 1450)]).Groups); Assert.Throws<ArgumentException>(() => SessionPlanner.Detect([M("a", 0), M("a", 0)])); }
}
