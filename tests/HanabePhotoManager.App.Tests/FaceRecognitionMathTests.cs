using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class FaceRecognitionMathTests
{
    [Fact]
    public void L2Normalize_ProducesUnitVectorWithoutChangingZeroVector()
    {
        var vector = new[] { 3f, 4f };
        FaceRecognitionMath.L2Normalize(vector);
        vector.Should().Equal(0.6f, 0.8f);

        var zero = new[] { 0f, 0f };
        FaceRecognitionMath.L2Normalize(zero);
        zero.Should().Equal(0f, 0f);
    }

    [Fact]
    public void Cosine_RejectsDifferentEmbeddingDimensions()
    {
        FaceRecognitionMath.Cosine([1f, 0f], [1f]).Should().Be(-1);
        FaceRecognitionMath.Cosine([1f, 0f], [1f, 0f]).Should().BeApproximately(1, 0.00001);
    }

    [Theory]
    [InlineData(FaceRecognitionEngineKind.YuNetSFace, 255, 255)]
    [InlineData(FaceRecognitionEngineKind.ArcFaceR100, 255, 1)]
    [InlineData(FaceRecognitionEngineKind.ArcFaceR100, 0, -1)]
    public void PrepareInputValue_SeparatesSFaceRawPixelsFromArcFaceNormalization(
        FaceRecognitionEngineKind engine, byte pixel, float expected)
    {
        FaceRecognitionMath.PrepareInputValue(pixel, engine).Should().BeApproximately(expected, 0.0001f);
    }

    [Fact]
    public void CorrectedSFacePreprocessing_UsesANewEmbeddingIdentity()
    {
        FaceModelIdentity.YuNetSFaceCurrent.StorageKey.Should().NotBe(FaceModelIdentity.YuNetSFaceLegacy.StorageKey);
        FaceModelIdentity.YuNetSFaceCurrent.EmbeddingVersion.Should().BeGreaterThan(
            FaceModelIdentity.YuNetSFaceLegacy.EmbeddingVersion);
    }

    [Fact]
    public void CurrentSFaceIdentity_UsesBalancedCosineThresholdForPoseVariation()
    {
        FaceModelIdentity.YuNetSFaceCurrent.MatchThreshold.Should().BeInRange(0.38, 0.45);
    }

    [Theory]
    [InlineData(0.60f, 120, 120, false)]
    [InlineData(0.74f, 120, 120, false)]
    [InlineData(0.80f, 120, 120, true)]
    [InlineData(0.95f, 18, 18, false)]
    [InlineData(0.95f, 120, 120, true)]
    public void FaceDetectionPolicy_RejectsLowConfidenceAndTinyTextureFalsePositives(
        float confidence, int width, int height, bool expected)
    {
        FaceRecognitionMath.IsAcceptableFaceDetection(confidence, width, height).Should().Be(expected);
    }

    [Theory]
    [InlineData(FaceRecognitionProfile.Speed, 640, 4, 16)]
    [InlineData(FaceRecognitionProfile.Balanced, 960, 2, 8)]
    [InlineData(FaceRecognitionProfile.HighAccuracy, 1280, 1, 4)]
    public void RuntimeProfile_BoundsImageConcurrencyAndBatchMemory(
        FaceRecognitionProfile profile, int edge, int concurrency, int batch)
    {
        var limits = FaceRuntimeLimits.For(profile, logicalProcessors: 8);
        limits.MaximumImageEdge.Should().Be(edge);
        limits.MaxConcurrency.Should().Be(concurrency);
        limits.BatchSize.Should().Be(batch);
    }
}
