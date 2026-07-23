using FluentAssertions;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Core.Tests.Imports;

public sealed class MediaClassifierTests
{
    public static TheoryData<string, MediaCategory, bool> ClassificationCases => new()
    {
        { @"D:\camera\photo.ARW", MediaCategory.Raw, false },
        { @"D:\camera\photo.cr2", MediaCategory.Raw, false },
        { @"D:\camera\photo.Cr3", MediaCategory.Raw, false },
        { @"D:\camera\photo.JPG", MediaCategory.Jpeg, false },
        { @"D:\camera\photo.jpeg", MediaCategory.Jpeg, false },
        { @"D:\camera\C0001.MP4", MediaCategory.Video, false },
        { @"D:\camera\c9999.mp4", MediaCategory.Video, false },
        { @"D:\camera\DJI_20260606171114_0004_D.MP4", MediaCategory.ActionVideo, false },
        { @"D:\camera\dji_20260606171114_0004_d.mp4", MediaCategory.ActionVideo, false },
        { @"D:\camera\holiday.MP4", MediaCategory.Video, false },
        { @"D:\camera\holiday.MOV", MediaCategory.Video, false },
        { @"D:\camera\holiday.MTS", MediaCategory.Video, false },
        { @"D:\camera\holiday.M2TS", MediaCategory.Video, false },
        { @"D:\camera\C0001M01.XML", MediaCategory.Video, false },
        { @"D:\camera\clip.LRF", MediaCategory.ActionVideo, false },
        { @"D:\camera\clip.AAC", MediaCategory.ActionVideo, false },
        { @"D:\camera\notes.txt", MediaCategory.Unconfirmed, true },
    };

    [Theory]
    [MemberData(nameof(ClassificationCases))]
    public void Classify_AppliesExpectedRule(
        string path,
        MediaCategory expectedCategory,
        bool requiresConfirmation)
    {
        var classifier = CreateClassifier();

        var result = classifier.Classify(CreateSource(path));

        result.SuggestedCategory.Should().Be(expectedCategory);
        result.RequiresConfirmation.Should().Be(requiresConfirmation);
        result.Rule.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Classify_PreservesSourceMetadata()
    {
        var source = new SourceMediaFile(
            @"D:\camera\photo.arw",
            1_234_567,
            new DateTimeOffset(2026, 6, 6, 17, 11, 14, TimeSpan.FromHours(8)));

        var result = CreateClassifier().Classify(source);

        result.File.Should().BeSameAs(source);
    }

    [Theory]
    [InlineData("DJI_2026060617111_0004_D.MP4")]
    [InlineData("DJI_202606061711140_0004_D.MP4")]
    [InlineData("DJI_20260606171114_004_D.MP4")]
    [InlineData("DJI_20260606171114_0004_X.MP4")]
    [InlineData("prefix_DJI_20260606171114_0004_D.MP4")]
    public void Classify_FallsBackToVideoForNonExactDjiFilename(string fileName)
    {
        var result = CreateClassifier().Classify(CreateSource(Path.Combine(@"D:\camera", fileName)));

        result.SuggestedCategory.Should().Be(MediaCategory.Video);
        result.RequiresConfirmation.Should().BeFalse();
    }

    [Theory]
    [InlineData("C٠٠٠١.MP4")]
    [InlineData("DJI_٢٠٢٦٠٦٠٦١٧١١١٤_0004_D.MP4")]
    public void Classify_FallsBackToVideoForNonAsciiCameraFilenames(string fileName)
    {
        var result = CreateClassifier().Classify(CreateSource(Path.Combine(@"D:\camera", fileName)));

        result.SuggestedCategory.Should().Be(MediaCategory.Video);
        result.RequiresConfirmation.Should().BeFalse();
    }

    [Fact]
    public void Constructor_AcceptsSingleRawExtensionTokensWithOrWithoutDot()
    {
        var classifier = new MediaClassifier(new[] { "ARW", ".cr2", "CR3", "DNG" });

        new[] { "photo.arw", "photo.CR2", "photo.cr3", "photo.dng" }
            .Select(name => classifier.Classify(CreateSource(Path.Combine(@"D:\camera", name))))
            .Should().OnlyContain(candidate =>
                candidate.SuggestedCategory == MediaCategory.Raw && !candidate.RequiresConfirmation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("*.ARW")]
    [InlineData(".AR*")]
    [InlineData(".raw.ARW")]
    [InlineData("folder/ARW")]
    [InlineData(@"folder\ARW")]
    public void Constructor_RejectsInvalidRawExtensionTokens(string extension)
    {
        var act = () => new MediaClassifier(new[] { extension });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*RAW extension*");
    }

    [Theory]
    [InlineData("JPG")]
    [InlineData(".jpeg")]
    [InlineData("MP4")]
    [InlineData(".mov")]
    [InlineData("MTS")]
    [InlineData(".m2ts")]
    [InlineData(".XML")]
    [InlineData("lrf")]
    [InlineData(".AAC")]
    public void Constructor_RejectsRawExtensionsThatConflictWithBuiltInTypes(string extension)
    {
        var act = () => new MediaClassifier(new[] { extension });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*RAW extension*");
    }

    [Theory]
    [InlineData("ARſ")]
    [InlineData("KRAW")]
    [InlineData("ＡRW")]
    public void Constructor_RejectsUnicodeLettersInRawExtensionTokens(string extension)
    {
        var act = () => new MediaClassifier(new[] { extension });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*RAW extension*");
    }

    private static MediaClassifier CreateClassifier()
    {
        return new MediaClassifier(new[] { ".ARW", ".CR2", ".CR3" });
    }

    private static SourceMediaFile CreateSource(string fullPath)
    {
        return new SourceMediaFile(fullPath, 42, DateTimeOffset.UnixEpoch);
    }
}
