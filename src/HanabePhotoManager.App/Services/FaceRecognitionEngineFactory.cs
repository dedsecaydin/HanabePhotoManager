namespace HanabePhotoManager.App.Services;

/// <summary>Windows entry point for the shared ONNX face engine.</summary>
public static class FaceRecognitionEngineFactory
{
    public static ILocalFaceEmbeddingService Create(FaceRecognitionOptions options) => PortableFaceRecognitionFactory.Create(options);
}
