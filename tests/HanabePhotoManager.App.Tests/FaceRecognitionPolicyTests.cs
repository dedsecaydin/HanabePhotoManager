using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;
using System.IO;

namespace HanabePhotoManager.App.Tests;

public sealed class FaceRecognitionPolicyTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-face-policy-{Guid.NewGuid():N}");

    [Fact]
    public void DefaultOptions_UseCompatibleYuNetSFaceBalancedMode()
    {
        var options = new FaceRecognitionOptions();

        options.Engine.Should().Be(FaceRecognitionEngineKind.YuNetSFace);
        options.Profile.Should().Be(FaceRecognitionProfile.Balanced);
        options.MatchThreshold.Should().Be(FaceRecognitionDefaults.YuNetSFaceThreshold);
        options.EvaluateAvailability().IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void ArcFace_IsDisabledUntilBothModelsAndExplicitLicenseArePresent()
    {
        Directory.CreateDirectory(_directory);
        var detector = Path.Combine(_directory, "detector.onnx");
        var recognizer = Path.Combine(_directory, "r100.onnx");
        File.WriteAllText(detector, "detector");
        File.WriteAllText(recognizer, "recognizer");
        var options = new FaceRecognitionOptions
        {
            Engine = FaceRecognitionEngineKind.ArcFaceR100,
            DetectorModelPath = detector,
            RecognizerModelPath = recognizer
        };

        options.EvaluateAvailability().Reason.Should().Contain("许可");
        options.ModelLicenseConfirmed = true;
        options.ModelLicenseDescription = "Internal model, training data and weights cleared for use.";
        options.EvaluateAvailability().IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void ModelIdentity_ChangesWhenEngineModelOrThresholdChanges()
    {
        Directory.CreateDirectory(_directory);
        var detector = Path.Combine(_directory, "detector.onnx");
        var recognizer = Path.Combine(_directory, "r100.onnx");
        File.WriteAllText(detector, "detector");
        File.WriteAllText(recognizer, "recognizer");
        var first = FaceModelIdentity.CreateArcFace(detector, recognizer, 0.45);

        File.AppendAllText(recognizer, "-v2");
        var second = FaceModelIdentity.CreateArcFace(detector, recognizer, 0.45);
        var third = FaceModelIdentity.CreateArcFace(detector, recognizer, 0.5);

        second.StorageKey.Should().NotBe(first.StorageKey);
        third.StorageKey.Should().NotBe(second.StorageKey);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
