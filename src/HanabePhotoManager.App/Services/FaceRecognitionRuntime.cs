namespace HanabePhotoManager.App.Services;

public sealed record FaceRuntimeLimits(int MaximumImageEdge, int MaxConcurrency, int BatchSize)
{
    public static FaceRuntimeLimits For(FaceRecognitionProfile profile, int? logicalProcessors = null)
    {
        var processors = Math.Max(1, logicalProcessors ?? Environment.ProcessorCount);
        return profile switch
        {
            FaceRecognitionProfile.Speed => new(640, Math.Min(4, processors), 16),
            FaceRecognitionProfile.HighAccuracy => new(1280, 1, 4),
            _ => new(960, Math.Min(2, processors), 8)
        };
    }
}

public static class FaceRecognitionMath
{
    public static bool IsAcceptableFaceDetection(float confidence, int width, int height) =>
        confidence >= 0.75f && width >= 24 && height >= 24;

    public static float PrepareInputValue(byte pixel, FaceRecognitionEngineKind engine) =>
        engine == FaceRecognitionEngineKind.ArcFaceR100
            ? (pixel - 127.5f) / 127.5f
            : pixel;

    public static void L2Normalize(float[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var squaredLength = vector.Sum(value => (double)value * value);
        if (squaredLength <= 1e-12) return;
        var inverseLength = 1d / Math.Sqrt(squaredLength);
        for (var index = 0; index < vector.Length; index++)
            vector[index] = (float)(vector[index] * inverseLength);
    }

    public static double Cosine(IReadOnlyList<float> first, IReadOnlyList<float> second)
    {
        if (first.Count == 0 || first.Count != second.Count) return -1;
        double dot = 0, firstLength = 0, secondLength = 0;
        for (var index = 0; index < first.Count; index++)
        {
            dot += first[index] * second[index];
            firstLength += first[index] * first[index];
            secondLength += second[index] * second[index];
        }
        return firstLength <= 0 || secondLength <= 0
            ? -1
            : dot / Math.Sqrt(firstLength * secondLength);
    }
}

public static class FaceBatchExecutor
{
    public static async Task<IReadOnlyList<TResult>> RunAsync<TSource, TResult>(
        IReadOnlyList<TSource> items,
        int maxConcurrency,
        int batchSize,
        Func<TSource, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        var concurrency = Math.Max(1, maxConcurrency);
        var boundedBatch = Math.Max(1, batchSize);
        var results = new TResult[items.Count];

        for (var offset = 0; offset < items.Count; offset += boundedBatch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = Math.Min(items.Count, offset + boundedBatch);
            var next = offset;
            var workers = Enumerable.Range(0, Math.Min(concurrency, end - offset)).Select(async _ =>
            {
                while (true)
                {
                    var index = Interlocked.Increment(ref next) - 1;
                    if (index >= end) break;
                    results[index] = await operation(items[index], cancellationToken).ConfigureAwait(false);
                }
            });
            await Task.WhenAll(workers).ConfigureAwait(false);
        }

        return results;
    }
}
