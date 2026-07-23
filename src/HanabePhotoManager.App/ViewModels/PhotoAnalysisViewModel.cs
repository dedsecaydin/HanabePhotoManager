using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

public sealed class PhotoAnalysisViewModel : ObservableObject
{
    private readonly IMediaMetadataStore _store;
    private readonly Func<string, IPhotoClassifier> _classifierFactory;
    private readonly IPhotoAnalysisCheckpointStore _checkpointStore;
    private CancellationTokenSource? _activeCancellation;
    private string _selectedEngine = PhotoClassifierFactory.OnnxMode;
    private bool _isAnalyzing;
    private double _progressValue;
    private string _statusText = "尚未开始智能识别";

    public PhotoAnalysisViewModel(
        IMediaMetadataStore store,
        Func<string, IPhotoClassifier>? classifierFactory = null,
        IPhotoAnalysisCheckpointStore? checkpointStore = null)
    {
        _store = store;
        _classifierFactory = classifierFactory ?? PhotoClassifierFactory.Create;
        _checkpointStore = checkpointStore ?? new PhotoAnalysisCheckpointStore();
        CancelCommand = new RelayCommand(Cancel, () => IsAnalyzing);
    }

    public IReadOnlyList<string> Engines { get; } =
        [PhotoClassifierFactory.RulesMode, PhotoClassifierFactory.OnnxMode, PhotoClassifierFactory.MobileClipMode];

    public string SelectedEngine
    {
        get => _selectedEngine;
        set => SetProperty(ref _selectedEngine, value ?? PhotoClassifierFactory.OnnxMode);
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set
        {
            if (SetProperty(ref _isAnalyzing, value)) CancelCommand.NotifyCanExecuteChanged();
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public IRelayCommand CancelCommand { get; }

    public async Task<PhotoAnalysisRunResult> AnalyzeAsync(
        IEnumerable<string> paths,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (IsAnalyzing) return new PhotoAnalysisRunResult(0, 0, 0, false);
        var files = paths.Where(File.Exists).Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Length == 0)
        {
            StatusText = "没有可分析的照片。";
            return new PhotoAnalysisRunResult(0, 0, 0, false);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeCancellation = linked;
        IsAnalyzing = true;
        ProgressValue = 0;
        var analyzed = 0;
        var cached = 0;
        var failed = 0;
        var cancelled = false;
        var snapshot = await _store.LoadAsync(linked.Token).ConfigureAwait(true);
        snapshot.Entries ??= [];
        var entries = snapshot.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
            .GroupBy(entry => Path.GetFullPath(entry.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var classifier = _classifierFactory(SelectedEngine);
        foreach (var checkpoint in await _checkpointStore.LoadAsync(linked.Token).ConfigureAwait(true))
        {
            var checkpointPath = Path.GetFullPath(checkpoint.Path);
            if (!entries.ContainsKey(checkpointPath)) snapshot.Entries.Add(checkpoint);
            entries[checkpointPath] = checkpoint;
        }

        try
        {
            for (var index = 0; index < files.Length; index++)
            {
                linked.Token.ThrowIfCancellationRequested();
                var path = files[index];
                var fingerprint = CreateFingerprint(path);
                if (!force && entries.TryGetValue(path, out var cachedEntry)
                    && cachedEntry.Fingerprint == fingerprint
                    && cachedEntry.ClassifierVersion == classifier.Version)
                {
                    cached++;
                }
                else
                {
                    try
                    {
                        var result = await classifier.ClassifyAsync(path, linked.Token).ConfigureAwait(true);
                        if (!entries.TryGetValue(path, out var entry))
                        {
                            entry = new MediaMetadataEntry { Path = path };
                            snapshot.Entries.Add(entry);
                            entries[path] = entry;
                        }
                        entry.Fingerprint = fingerprint;
                        entry.AutomaticLabels = result.Labels.ToList();
                        entry.ClassifierVersion = result.EngineVersion;
                        entry.AnalyzedAt = DateTimeOffset.Now;
                        await _checkpointStore.AppendAsync(entry, linked.Token).ConfigureAwait(true);
                        analyzed++;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch
                    {
                        failed++;
                    }
                }

                ProgressValue = (index + 1) * 100d / files.Length;
                StatusText = $"正在识别 {index + 1}/{files.Length} · 已完成 {analyzed} · 缓存 {cached} · 失败 {failed}";
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            StatusText = $"识别已停止 · 已完成 {analyzed} · 缓存 {cached} · 失败 {failed}";
        }
        finally
        {
            await _store.SaveAsync(snapshot).ConfigureAwait(true);
            await _checkpointStore.ClearAsync().ConfigureAwait(true);
            if (!cancelled)
            {
                ProgressValue = 100;
                StatusText = $"识别完成 · 新分析 {analyzed} · 使用缓存 {cached} · 失败 {failed}";
            }
            if (classifier is IDisposable disposable) disposable.Dispose();
            _activeCancellation = null;
            IsAnalyzing = false;
        }

        return new PhotoAnalysisRunResult(analyzed, cached, failed, cancelled);
    }

    private void Cancel() => _activeCancellation?.Cancel();

    private static string CreateFingerprint(string path)
    {
        var info = new FileInfo(path);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }
}

public sealed record PhotoAnalysisRunResult(int Analyzed, int Cached, int Failed, bool Cancelled);
