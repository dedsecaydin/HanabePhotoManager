namespace HanabePhotoManager.App.Services;

/// <summary>把用户性能配置转换为识别运行时限制。</summary>
public static class FaceRecognitionRuntimeOptions
{
    private static FaceRecognitionOptions _current = new();
    public static FaceRecognitionOptions Current
    {
        get => Volatile.Read(ref _current);
        set => Volatile.Write(ref _current, value ?? throw new ArgumentNullException(nameof(value)));
    }

    public static FaceModelIdentity CurrentIdentity
    {
        get
        {
            var options = Current;
            if (options.Engine == FaceRecognitionEngineKind.ArcFaceR100
                && options.EvaluateAvailability().IsAvailable)
                return FaceModelIdentity.CreateArcFace(
                    options.DetectorModelPath!, options.RecognizerModelPath!,
                    options.MatchThreshold <= 0 ? FaceRecognitionDefaults.ArcFaceR100Threshold : options.MatchThreshold);
            return FaceModelIdentity.YuNetSFaceCurrent;
        }
    }
}

