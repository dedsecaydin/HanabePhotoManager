using System.Diagnostics;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class FaceRecognitionPerformanceTests
{
    [Fact]
    public async Task BoundedExecutor_ReusesWorkersAndNeverExceedsConfiguredConcurrency()
    {
        var active = 0;
        var peak = 0;
        var started = Stopwatch.StartNew();
        var results = await FaceBatchExecutor.RunAsync(
            Enumerable.Range(0, 24).ToArray(),
            maxConcurrency: 3,
            batchSize: 6,
            async (value, token) =>
            {
                var current = Interlocked.Increment(ref active);
                InterlockedExtensions.Max(ref peak, current);
                await Task.Delay(5, token);
                Interlocked.Decrement(ref active);
                return value * 2;
            },
            default);

        results.Should().HaveCount(24);
        peak.Should().BeInRange(1, 3);
        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task BoundedExecutor_StopsOnCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Func<Task> act = async () => await FaceBatchExecutor.RunAsync(
            Enumerable.Range(0, 100).ToArray(), 2, 4,
            (value, token) => Task.FromResult(value), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            var current = Volatile.Read(ref target);
            while (value > current)
            {
                var observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current) return;
                current = observed;
            }
        }
    }
}
