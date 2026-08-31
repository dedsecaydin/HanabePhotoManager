using FluentAssertions;
using HanabePhotoManager.Core.Recovery;
using Xunit;

namespace HanabePhotoManager.Core.Tests.Recovery;

public sealed class RecoverySafetyPolicyTests
{
    [Theory]
    [InlineData("card.img", true)]
    [InlineData("card.RAW", true)]
    [InlineData("card.iso", false)]
    public void SupportedImages_AreRestrictedToRawFormats(string path, bool expected) =>
        RecoverySafetyPolicy.IsSupportedImage(path).Should().Be(expected);

    [Fact]
    public void ExperimentalCandidate_CannotBeExported()
    {
        var candidate = new RecoveryCandidate("x", "x.mp4", 0, 100, true, true, false, false, false, RecoveryConfidence.Medium, RecoveryCandidateStatus.Experimental);
        RecoverySafetyPolicy.CanExport(candidate, 100).Should().BeFalse();
    }
}
