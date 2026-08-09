using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.Core.Search;

namespace HanabePhotoManager.App.Search;

public sealed class SemanticSearchViewModel : ObservableObject, IDisposable
{
    public const int ResultLimit = 50;
    private readonly ISemanticSearchService _service;
    private readonly Func<string> _libraryRoot;
    private readonly TimeSpan _searchDebounce;
    private CancellationTokenSource? _operationCancellation;
    private bool _indexEnsured;
    private int _lastPublishedIndexedFiles;
    private bool _progressiveSearchActive;
    private string _queryText = string.Empty;
    private string _statusText = "输入描述即可在本机照片库中查找。";
    private double _progressValue;
    private bool _isBusy;

    public SemanticSearchViewModel(
        ISemanticSearchService service,
        Func<string> libraryRoot,
        TimeSpan? searchDebounce = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _libraryRoot = libraryRoot ?? throw new ArgumentNullException(nameof(libraryRoot));
        _searchDebounce = searchDebounce ?? TimeSpan.FromMilliseconds(300);
        ReindexCommand = new AsyncRelayCommand(ReindexAsync, CanReindex);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        OpenResultCommand = new RelayCommand<SearchResultItemViewModel>(item => item?.Open());
        OpenResultFolderCommand = new RelayCommand<SearchResultItemViewModel>(item => item?.OpenFolder());
        RefreshStatus();
    }

    public ObservableCollection<SearchResultItemViewModel> Results { get; } = [];
    public event EventHandler? ResultsChanged;
    public IAsyncRelayCommand ReindexCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand<SearchResultItemViewModel> OpenResultCommand { get; }
    public IRelayCommand<SearchResultItemViewModel> OpenResultFolderCommand { get; }
    public string QueryText
    {
        get => _queryText;
        set
        {
            if (SetProperty(ref _queryText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasActiveQuery));
                Results.Clear();
                NotifyResultsChanged();
                _ = DebouncedSearchAsync(_queryText);
            }
        }
    }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public double ProgressValue { get => _progressValue; private set => SetProperty(ref _progressValue, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) { CancelCommand.NotifyCanExecuteChanged(); ReindexCommand.NotifyCanExecuteChanged(); } } }
    public bool HasResults => Results.Count > 0;
    public bool HasActiveQuery => !string.IsNullOrWhiteSpace(QueryText);
    public IReadOnlyList<string> RankedResultPaths => Results.Select(result => result.FilePath).ToArray();
    public string ResultSummary => HasResults ? $"找到 {Results.Count:N0} 张相关照片" : "尚未找到结果";

    public void NotifyLibraryRootChanged()
    {
        _indexEnsured = false;
        ReindexCommand.NotifyCanExecuteChanged();
    }

    private async Task DebouncedSearchAsync(string query)
    {
        _operationCancellation?.Cancel();
        if (string.IsNullOrWhiteSpace(query)) { Results.Clear(); NotifyResultsChanged(); RefreshStatus(); return; }
        var cancellation = _operationCancellation = new CancellationTokenSource();
        try
        {
            await Task.Delay(_searchDebounce, cancellation.Token).ConfigureAwait(true);
            IsBusy = true;
            if (!_indexEnsured)
            {
                _lastPublishedIndexedFiles = 0;
                var progress = new Progress<SemanticIndexStatus>(status => UpdateIndexProgress(query, status, cancellation));
                await Task.Run(
                    () => _service.EnsureIndexAsync(_libraryRoot(), progress, cancellation.Token),
                    cancellation.Token).ConfigureAwait(true);
                _indexEnsured = true;
            }
            var rankedMatches = await SearchAndPublishAsync(query, cancellation.Token).ConfigureAwait(true);
            StatusText = rankedMatches.Length == 0 ? "没找到相关照片，换个描述试试。" : "已按语义相关度排序。";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Results.Clear(); NotifyResultsChanged(); StatusText = ex.Message; }
        finally { if (ReferenceEquals(_operationCancellation, cancellation)) { IsBusy = false; cancellation.Dispose(); _operationCancellation = null; } }
    }

    private async Task ReindexAsync()
    {
        if (!CanReindex()) return;
        _operationCancellation?.Cancel();
        var cancellation = _operationCancellation = new CancellationTokenSource();
        IsBusy = true; ProgressValue = 0;
        try
        {
            var progress = new Progress<SemanticIndexStatus>(status => { ProgressValue = status.ProgressPercent; StatusText = status.Message; });
            await Task.Run(
                () => _service.EnsureIndexAsync(_libraryRoot(), progress, cancellation.Token),
                cancellation.Token).ConfigureAwait(true);
            _indexEnsured = true;
            RefreshStatus();
            if (!string.IsNullOrWhiteSpace(QueryText)) await DebouncedSearchAsync(QueryText).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { StatusText = "语义索引已停止。"; }
        catch (Exception ex) { StatusText = ex.Message; }
        finally { if (ReferenceEquals(_operationCancellation, cancellation)) { IsBusy = false; cancellation.Dispose(); _operationCancellation = null; } }
    }

    private bool CanReindex() => !IsBusy && Directory.Exists(_libraryRoot());
    private void UpdateIndexProgress(string query, SemanticIndexStatus status, CancellationTokenSource cancellation)
    {
        ProgressValue = status.ProgressPercent;
        StatusText = status.Message;
        if (!status.IsIndexing || status.IndexedFiles <= _lastPublishedIndexedFiles || cancellation.IsCancellationRequested) return;
        _lastPublishedIndexedFiles = status.IndexedFiles;
        if (_progressiveSearchActive) return;
        _progressiveSearchActive = true;
        _ = PublishIndexedBatchAsync(query, cancellation.Token);
    }

    private async Task PublishIndexedBatchAsync(string query, CancellationToken cancellationToken)
    {
        try { await SearchAndPublishAsync(query, cancellationToken).ConfigureAwait(true); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { StatusText = ex.Message; }
        finally { _progressiveSearchActive = false; }
    }

    private async Task<SemanticSearchResult[]> SearchAndPublishAsync(string query, CancellationToken cancellationToken)
    {
        var matches = await Task.Run(
            () => _service.SearchAsync(query.Trim(), ResultLimit, cancellationToken),
            cancellationToken).ConfigureAwait(true);
        var rankedMatches = matches.OrderByDescending(match => match.Score)
            .ThenBy(match => match.FileKey, StringComparer.OrdinalIgnoreCase)
            .GroupBy(match => match.FileKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).Take(ResultLimit).ToArray();
        Results.Clear();
        foreach (var match in rankedMatches) Results.Add(new SearchResultItemViewModel(match));
        NotifyResultsChanged();
        return rankedMatches;
    }

    private void Cancel() => _operationCancellation?.Cancel();
    private void RefreshStatus() { var status = _service.GetIndexStatus(); ProgressValue = status.ProgressPercent; StatusText = status.Message; }
    private void NotifyResultsChanged()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ResultSummary));
        OnPropertyChanged(nameof(RankedResultPaths));
        ResultsChanged?.Invoke(this, EventArgs.Empty);
    }
    public void Dispose() { _operationCancellation?.Cancel(); _operationCancellation?.Dispose(); }
}
