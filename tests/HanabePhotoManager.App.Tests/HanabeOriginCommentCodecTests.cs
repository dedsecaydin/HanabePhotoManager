using FluentAssertions;
using HanabePhotoManager.App.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class HanabeOriginCommentCodecTests
{
    private static readonly FileOriginMetadata Metadata = new("DSC_1234.MP4", 123456, new string('A', 64));

    [Fact]
    public void MergeAndParse_RoundTripsAndPreservesUserComment()
    {
        var merged = HanabeOriginCommentCodec.Merge("我的视频备注", Metadata);

        merged.Should().StartWith("我的视频备注\nHanabe:v1;");
        HanabeOriginCommentCodec.TryParse(merged, out var parsed).Should().BeTrue();
        parsed.Should().Be(Metadata);
    }

    [Fact]
    public void Merge_ReplacesExistingHanabeLineWithoutDuplication()
    {
        var old = HanabeOriginCommentCodec.Merge("用户备注", new("old.jpg", 1, new string('B', 64)));
        var updated = HanabeOriginCommentCodec.Merge(old, Metadata);

        updated.Split("Hanabe:v1;").Should().HaveCount(2);
        updated.Should().Contain("用户备注").And.NotContain("old.jpg");
    }

    [Fact]
    public void Codec_EscapesReservedCharactersInOriginalName()
    {
        var special = Metadata with { OriginalName = "DSC;12=34 %25.JPG" };
        var text = HanabeOriginCommentCodec.Merge(null, special);

        HanabeOriginCommentCodec.TryParse(text, out var parsed).Should().BeTrue();
        parsed.Should().Be(special);
    }

    [Theory]
    [InlineData("Hanabe:v1;OriginalName=a.jpg;OriginalLength=x;OriginalSha256=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("Hanabe:v1;OriginalName=a.jpg;OriginalLength=1;OriginalSha256=bad")]
    [InlineData("ordinary comment")]
    public void TryParse_RejectsMalformedOrMissingIdentity(string text)
    {
        HanabeOriginCommentCodec.TryParse(text, out _).Should().BeFalse();
    }
}
