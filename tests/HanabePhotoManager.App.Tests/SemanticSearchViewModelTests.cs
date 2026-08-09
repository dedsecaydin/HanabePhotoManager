using HanabePhotoManager.App.Search;
using HanabePhotoManager.Core.Search;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class SemanticSearchViewModelTests
{
    [Fact]
    public async Task FirstQuery_EnsuresIndexBeforeSearchingAndPublishesRankedPaths()
    {
        var service = new RecordingSemanticSearchService(
            [new SemanticSearchResult(@"D:\photos\second.jpg", 0.91),
             new SemanticSearchResult(@"D:\photos\first.jpg", 0.83)]);
        using var viewModel = new SemanticSearchViewModel(service, () => @"D:\photos", TimeSpan.Zero);
        var resultsChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.ResultsChanged += (_, _) => resultsChanged.TrySetResult();

        viewModel.QueryText = "红色衣服";

        await resultsChanged.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(["ensure", "search"], service.Calls);
        Assert.Equal([@"D:\photos\second.jpg", @"D:\photos\first.jpg"], viewModel.RankedResultPaths);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task ClearingQuery_PublishesAnEmptyCandidateSet()
    {
        var service = new RecordingSemanticSearchService(
            [new SemanticSearchResult(@"D:\photos\photo.jpg", 0.9)]);
        using var viewModel = new SemanticSearchViewModel(service, () => @"D:\photos", TimeSpan.Zero);
        var changeCount = 0;
        var cleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.ResultsChanged += (_, _) =>
        {
            changeCount++;
            if (changeCount == 2) cleared.TrySetResult();
        };

        viewModel.QueryText = "海边日落";
        await WaitUntilAsync(() => viewModel.RankedResultPaths.Count == 1);
        viewModel.QueryText = string.Empty;

        await cleared.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Empty(viewModel.RankedResultPaths);
        Assert.False(viewModel.HasActiveQuery);
    }

    [Fact]
    public async Task FirstQuery_DoesNotRunIndexingWorkOnTheCallingThread()
    {
        var service = new BlockingSemanticSearchService();
        using var viewModel = new SemanticSearchViewModel(service, () => @"D:\photos", TimeSpan.Zero);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        viewModel.QueryText = "红色衣服";
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(100));
        await service.IndexStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(predicate());
    }

    private sealed class RecordingSemanticSearchService(IReadOnlyList<SemanticSearchResult> results) : ISemanticSearchService
    {
        public List<string> Calls { get; } = [];

        public Task EnsureIndexAsync(string libraryRoot, IProgress<SemanticIndexStatus>? progress, CancellationToken cancellationToken)
        {
            Calls.Add("ensure");
            progress?.Report(new SemanticIndexStatus(1, 1, false, true, "索引已就绪"));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
        {
            Calls.Add("search");
            return Task.FromResult(results);
        }

        public SemanticIndexStatus GetIndexStatus() => new(0, 0, false, true, "等待搜索");
    }

    private sealed class BlockingSemanticSearchService : ISemanticSearchService
    {
        public TaskCompletionSource IndexStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task EnsureIndexAsync(string libraryRoot, IProgress<SemanticIndexStatus>? progress, CancellationToken cancellationToken)
        {
            IndexStarted.TrySetResult();
            Thread.Sleep(250);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SemanticSearchResult>>([]);

        public SemanticIndexStatus GetIndexStatus() => new(0, 0, false, true, "等待搜索");
    }
}
