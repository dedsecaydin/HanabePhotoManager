using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PhotoAnalysisViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-analysis-{Guid.NewGuid():N}");

    [Fact]
    public async Task AnalyzeAsync_ContinuesAfterFailureAndReportsProgress()
    {
        var paths = CreateFiles("a.jpg", "bad.jpg", "c.jpg");
        var classifier = new RecordingClassifier(path => Path.GetFileName(path) == "bad.jpg");
        var viewModel = CreateViewModel(classifier);

        var result = await viewModel.AnalyzeAsync(paths);

        result.Analyzed.Should().Be(2);
        result.Failed.Should().Be(1);
        viewModel.ProgressValue.Should().Be(100);
        viewModel.StatusText.Should().Contain("2").And.Contain("1");
    }

    [Fact]
    public async Task AnalyzeAsync_CacheHitSkipsClassifierUntilFingerprintChanges()
    {
        var path = CreateFiles("a.jpg").Single();
        var classifier = new RecordingClassifier();
        var viewModel = CreateViewModel(classifier);

        await viewModel.AnalyzeAsync([path]);
        await viewModel.AnalyzeAsync([path]);
        classifier.CallCount.Should().Be(1);

        await File.AppendAllTextAsync(path, "changed");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));
        await viewModel.AnalyzeAsync([path]);
        classifier.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task AnalyzeAsync_PreservesManualCategoryAndTags()
    {
        var path = CreateFiles("a.jpg").Single();
        var store = CreateStore();
        await store.UpsertAsync(new MediaMetadataEntry
        {
            Path = path,
            ManualCategory = "我的类别",
            ManualTags = ["家庭"]
        });
        var viewModel = new PhotoAnalysisViewModel(store, _ => new RecordingClassifier());

        await viewModel.AnalyzeAsync([path], force: true);

        var entry = await store.GetAsync(path);
        entry!.ManualCategory.Should().Be("我的类别");
        entry.ManualTags.Should().Equal("家庭");
        entry.AutomaticLabels.Should().ContainSingle(label => label.Label == "自然风景");
    }

    [Fact]
    public async Task CancelCommand_StopsActiveBatchAndKeepsCompletedResults()
    {
        var paths = CreateFiles("a.jpg", "b.jpg", "c.jpg");
        var classifier = new RecordingClassifier(delay: TimeSpan.FromMilliseconds(120));
        var viewModel = CreateViewModel(classifier);

        var task = viewModel.AnalyzeAsync(paths);
        await Task.Delay(40);
        viewModel.CancelCommand.Execute(null);
        var result = await task;

        result.Cancelled.Should().BeTrue();
        classifier.CallCount.Should().BeLessThan(paths.Length);
        viewModel.IsAnalyzing.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_CheckpointsAfterEveryProcessedPhoto()
    {
        var paths = CreateFiles("a.jpg", "b.jpg", "c.jpg");
        var checkpoint = new CountingCheckpoint();
        var viewModel = new PhotoAnalysisViewModel(CreateStore(), _ => new RecordingClassifier(), checkpoint);

        await viewModel.AnalyzeAsync(paths);

        checkpoint.AppendCount.Should().Be(paths.Length);
    }

    private PhotoAnalysisViewModel CreateViewModel(IPhotoClassifier classifier) =>
        new(CreateStore(), _ => classifier);

    private MediaMetadataStore CreateStore() => new(Path.Combine(_directory, "metadata.json"));

    private string[] CreateFiles(params string[] names)
    {
        Directory.CreateDirectory(_directory);
        return names.Select(name =>
        {
            var path = Path.Combine(_directory, name);
            File.WriteAllText(path, name);
            return path;
        }).ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class RecordingClassifier : IPhotoClassifier
    {
        private readonly Func<string, bool> _shouldFail;
        private readonly TimeSpan _delay;
        public RecordingClassifier(Func<string, bool>? shouldFail = null, TimeSpan? delay = null)
        {
            _shouldFail = shouldFail ?? (_ => false);
            _delay = delay ?? TimeSpan.Zero;
        }

        public int CallCount { get; private set; }
        public string EngineId => "test-engine";
        public string Version => "test-1";

        public async Task<PhotoClassificationResult> ClassifyAsync(string path, CancellationToken cancellationToken)
        {
            CallCount++;
            if (_delay > TimeSpan.Zero) await Task.Delay(_delay, cancellationToken);
            if (_shouldFail(path)) throw new InvalidDataException("fixture failure");
            return new PhotoClassificationResult(
                [new PhotoLabelScore("自然风景", 0.9)], EngineId, Version, "fixture");
        }
    }

    private sealed class CountingStore(IMediaMetadataStore inner) : IMediaMetadataStore
    {
        public int SaveCount { get; private set; }
        public Task<MediaMetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default) => inner.LoadAsync(cancellationToken);
        public Task<MediaMetadataEntry?> GetAsync(string path, CancellationToken cancellationToken = default) => inner.GetAsync(path, cancellationToken);
        public Task UpsertAsync(MediaMetadataEntry entry, CancellationToken cancellationToken = default) => inner.UpsertAsync(entry, cancellationToken);
        public async Task SaveAsync(MediaMetadataSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            await inner.SaveAsync(snapshot, cancellationToken);
        }
    }

    private sealed class CountingCheckpoint : IPhotoAnalysisCheckpointStore
    {
        public int AppendCount { get; private set; }
        public Task<IReadOnlyList<MediaMetadataEntry>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MediaMetadataEntry>>([]);
        public Task AppendAsync(MediaMetadataEntry entry, CancellationToken cancellationToken = default) { AppendCount++; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
