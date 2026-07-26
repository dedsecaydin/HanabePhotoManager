using Microsoft.ML.OnnxRuntime;

namespace HanabePhotoManager.App.Services;

public static class OnnxRuntimeSessionFactory
{
    public static bool TryCreateDirectMlOptions(out SessionOptions? options)
    {
        options = null;
        try
        {
            var candidate = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
            };
            candidate.AppendExecutionProvider_DML(0);
            options = candidate;
            return true;
        }
        catch (Exception ex) when (ex is OnnxRuntimeException or DllNotFoundException or EntryPointNotFoundException)
        {
            options?.Dispose();
            options = null;
            return false;
        }
    }

    public static InferenceSession Create(string modelPath)
    {
        if (!string.Equals(MobileClipRuntimeOptions.DevicePreference, "CPU", StringComparison.OrdinalIgnoreCase)
            && TryCreateDirectMlOptions(out var options))
        {
            using (options)
            {
                return new InferenceSession(modelPath, options);
            }
        }

        return new InferenceSession(modelPath);
    }
}
