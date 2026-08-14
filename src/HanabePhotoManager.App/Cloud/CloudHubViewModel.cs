using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.App.Cloud;

public sealed class CloudHubViewModel : ObservableObject, IDisposable
{
    private const string ThumbnailCachePrefix = "cloud-thumbnail";

    private readonly ICloudProvider _provider;
    private readonly ICloudIndexStore _index;
    private readonly ICloudCacheStore _cache;
    private readonly ICloudTransferQueueStore? _transferQueueStore;
    private readonly SynchronizationContext _synchronizationContext;
    private readonly object _operationGate = new();
    private CancellationTokenSource? _activeOperation;
    private int _disposed;
    private long _operationGeneration;
    private DateTimeOffset _operationStartedAt;
    private CloudAccountState _accountState;
    private CloudPath _currentPath = new("/");
    private bool _isBusy;
    private bool _isProgressIndeterminate;
    private double _progressValue;
    private int _scannedItemCount;
    private string _progressText = "等待扫描。";
    private string _statusText = "等待连接云盘。";
    private string? _errorMessage;
    private string? _selectedPreviewPath;
    private string _diagnosticsText = "等待诊断…";

    public CloudHubViewModel(
        ICloudProvider provider,
        ICloudIndexStore index,
        ICloudCacheStore cache,
        SynchronizationContext? synchronizationContext = null,
        ICloudTransferQueueStore? transferQueueStore = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _transferQueueStore = transferQueueStore;
        _synchronizationContext = synchronizationContext ??
            SynchronizationContext.Current ??
            throw new InvalidOperationException(
                "A UI SynchronizationContext is required to create the cloud hub view model.");
        _accountState = new CloudAccountState(
            provider.Kind,
            false,
            "未连接",
            0,
            0,
            "等待连接");

        RefreshCommand = new AsyncRelayCommand(
            token => RefreshAsync(token),
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        OpenItemCommand = new AsyncRelayCommand<CloudObjectItemViewModel>(
            (item, token) => item is null ? Task.CompletedTask : OpenItemAsync(item, token),
            item => item is not null,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        BackCommand = new AsyncRelayCommand(
            token => BackAsync(token),
            CanNavigateBack,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelCurrentOperationCommand = new RelayCommand(
            CancelCurrentOperation,
            () => IsBusy);
    }

    public ObservableCollection<CloudObjectItemViewModel> Items { get; } = [];

    public CloudAccountState AccountState
    {
        get => _accountState;
        private set
        {
            if (SetProperty(ref _accountState, value))
            {
                OnPropertyChanged(nameof(AccountTitle));
                OnPropertyChanged(nameof(UsedPercent));
                OnPropertyChanged(nameof(UsedPercentText));
                OnPropertyChanged(nameof(UsedBytesText));
                OnPropertyChanged(nameof(CapacityText));
                OnPropertyChanged(nameof(HasCapacityInfo));
                OnPropertyChanged(nameof(IsAccountConnected));
                OnPropertyChanged(nameof(AccountBadgeText));
                OnPropertyChanged(nameof(AccountSubtitle));
            }
        }
    }

    /// <summary>Account display name (e.g. "百度网盘" / "夸克网盘").</summary>
    public string AccountTitle => AccountState.DisplayName;

    /// <summary>
    /// Live account hint shown under the account name: the current operation
    /// status while connected, or the honest "not logged in / not integrated"
    /// reason while no data source is available.
    /// </summary>
    public string AccountSubtitle => AccountState.IsAuthenticated
        ? StatusText
        : AccountState.StatusText;

    /// <summary>Short connection badge text ("已连接" / "未接入").</summary>
    public string AccountBadgeText => AccountState.IsAuthenticated ? "已连接" : "未接入";

    public bool IsAccountConnected => AccountState.IsAuthenticated;

    /// <summary>Usage fraction in [0, 1]; zero when no capacity information exists.</summary>
    public double UsedPercent => AccountState.TotalBytes > 0
        ? Math.Clamp(AccountState.UsedBytes / (double)AccountState.TotalBytes, 0, 1)
        : 0;

    /// <summary>Compact percentage label ("68%"), "—" when unknown.</summary>
    public string UsedPercentText => AccountState.TotalBytes > 0
        ? $"{UsedPercent * 100:0}%"
        : "—";

    /// <summary>Human-readable used capacity ("214.6 GB"), "—" when unknown.</summary>
    public string UsedBytesText => AccountState.TotalBytes > 0
        ? FormatBytes(AccountState.UsedBytes)
        : "—";

    /// <summary>Total / remaining capacity line, or an honest "no data" note.</summary>
    public string CapacityText => AccountState.TotalBytes > 0
        ? $"总容量 {FormatBytes(AccountState.TotalBytes)} · 剩余 {FormatBytes(AccountState.TotalBytes - AccountState.UsedBytes)}"
        : "暂无容量信息";

    public bool HasCapacityInfo => AccountState.TotalBytes > 0;

    /// <summary>Transfer jobs loaded from the local queue store (empty when none).</summary>
    public ObservableCollection<CloudTransferJobItemViewModel> TransferJobs { get; } = [];

    public bool HasTransferJobs => TransferJobs.Count > 0;

    /// <summary>Jobs currently in a running / pending / verifying state.</summary>
    public int ActiveTransferCount => TransferJobs.Count(job => job.IsActive);

    public CloudPath CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (SetProperty(ref _currentPath, value))
            {
                BackCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CancelCurrentOperationCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set
        {
            if (SetProperty(ref _progressValue, Math.Clamp(value, 0, 1)))
            {
                OnPropertyChanged(nameof(EstimatedTimeRemaining));
            }
        }
    }

    public string EstimatedTimeRemaining
    {
        get
        {
            if (_isProgressIndeterminate || _progressValue <= 0 || _progressValue >= 1)
            {
                return string.Empty;
            }

            var elapsed = DateTimeOffset.UtcNow - _operationStartedAt;
            if (elapsed <= TimeSpan.Zero)
            {
                return string.Empty;
            }

            var total = elapsed.TotalSeconds / _progressValue;
            var remaining = TimeSpan.FromSeconds(total - elapsed.TotalSeconds);
            return remaining switch
            {
                { TotalMinutes: >= 1 } => $"剩余约 {remaining.TotalMinutes:0} 分钟",
                { TotalSeconds: >= 5 } => $"剩余约 {remaining.TotalSeconds:0} 秒",
                _ => "即将完成"
            };
        }
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public int ScannedItemCount
    {
        get => _scannedItemCount;
        private set => SetProperty(ref _scannedItemCount, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetProperty(ref _statusText, value))
            {
                OnPropertyChanged(nameof(AccountSubtitle));
            }
        }
    }

    public string? SelectedPreviewPath
    {
        get => _selectedPreviewPath;
        private set => SetProperty(ref _selectedPreviewPath, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => ErrorMessage is not null;

    public string DiagnosticsText
    {
        get => _diagnosticsText;
        private set => SetProperty(ref _diagnosticsText, value);
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand<CloudObjectItemViewModel> OpenItemCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    public IRelayCommand CancelCurrentOperationCommand { get; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        ExecuteLatestAsync(
            async (generation, token) =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var t0 = sw.ElapsedMilliseconds;

                var account = await _provider.GetAccountStateAsync(token);
                var t1 = sw.ElapsedMilliseconds;
                token.ThrowIfCancellationRequested();
                await ApplyIfCurrentAsync(generation, () =>
                {
                    AccountState = account;
                    StatusText = account.IsAuthenticated
                        ? $"已连接 {account.DisplayName}，正在扫描云端内容…"
                        : $"{account.DisplayName} 尚未登录，请先完成登录。";
                });

                if (_transferQueueStore is not null)
                {
                    var jobs = await _transferQueueStore.LoadAsync(token);
                    token.ThrowIfCancellationRequested();
                    await ApplyIfCurrentAsync(generation, () =>
                    {
                        TransferJobs.Clear();
                        foreach (var job in jobs)
                        {
                            TransferJobs.Add(new CloudTransferJobItemViewModel(job));
                        }

                        OnPropertyChanged(nameof(HasTransferJobs));
                        OnPropertyChanged(nameof(ActiveTransferCount));
                    });
                }

                if (!account.IsAuthenticated)
                {
                    await ApplyIfCurrentAsync(generation, () =>
                    {
                        CurrentPath = new CloudPath("/");
                        Items.Clear();
                        SelectedPreviewPath = null;
                        ProgressValue = 0;
                        ScannedItemCount = 0;
                        IsProgressIndeterminate = false;
                        ProgressText = "尚未登录，请先完成登录。";
                    });
                    DiagnosticsText = $"GetAccountStateAsync 耗时 {t1 - t0}ms · 未授权无需 ListAsync";
                    return;
                }

                var root = new CloudPath("/");
                var tt = System.Diagnostics.Stopwatch.StartNew();
                await PrepareScanAsync(generation, root);
                var items = await ListAndIndexAsync(root, generation, token);
                var t2 = tt.ElapsedMilliseconds;
                token.ThrowIfCancellationRequested();

                await ApplyIfCurrentAsync(generation, () =>
                {
                    CurrentPath = root;
                    SelectedPreviewPath = null;
                    ProgressValue = 1;
                    IsProgressIndeterminate = false;
                    ProgressText = $"扫描完成，共 {items.Count} 项。";
                    StatusText = $"已连接 {account.DisplayName}，共 {items.Count} 项。";

                    var usedMb = account.UsedBytes / (1024.0 * 1024.0);
                    var totalMb = account.TotalBytes / (1024.0 * 1024.0);
                    DiagnosticsText = $"⏱ GetAccountState={(t1 - t0)}ms · List={t2}ms · 总计 {items.Count} 项 · 容量 {usedMb:0}MB / {totalMb:0}MB";
                });
            },
            "正在连接云盘并读取根目录…",
            cancellationToken);

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var path = CurrentPath;
        return NavigateAsync(path, "正在刷新当前目录…", cancellationToken);
    }

    public Task OpenFolderAsync(
        CloudObjectItemViewModel folder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (!folder.IsFolder)
        {
            throw new ArgumentException("The selected cloud object is not a folder.", nameof(folder));
        }

        return NavigateAsync(folder.Path, $"正在打开 {folder.Name}…", cancellationToken);
    }

    public Task OpenItemAsync(
        CloudObjectItemViewModel item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsFolder)
        {
            return OpenFolderAsync(item, cancellationToken);
        }

        return ExecuteLatestAsync(
            async (generation, token) =>
            {
                await ApplyIfCurrentAsync(generation, () => SelectedPreviewPath = null);
                var key = CreateThumbnailCacheKey(item.Provider, item.RemoteId);
                var cachedPath = await _cache.TryGetAsync(key, token);
                token.ThrowIfCancellationRequested();
                if (cachedPath is not null && IsReadableCacheFile(cachedPath))
                {
                    await ApplyIfCurrentAsync(generation, () =>
                    {
                        SelectedPreviewPath = cachedPath;
                        ProgressValue = 1;
                        StatusText = $"已从缓存打开 {item.Name}。";
                    });
                    return;
                }

                await using var thumbnail = await _provider.OpenThumbnailAsync(item.Source, token);
                token.ThrowIfCancellationRequested();
                if (thumbnail is null)
                {
                    await ApplyIfCurrentAsync(generation, () =>
                    {
                        ProgressValue = 1;
                        StatusText = $"{item.Name} 暂无可用缩略图。";
                    });
                    return;
                }

                var previewPath = await _cache.PutAsync(key, thumbnail, false, token);
                token.ThrowIfCancellationRequested();
                await ApplyIfCurrentAsync(generation, () =>
                {
                    SelectedPreviewPath = previewPath;
                    ProgressValue = 1;
                    StatusText = $"已打开 {item.Name}。";
                });
            },
            $"正在读取 {item.Name} 的缩略图…",
            cancellationToken);
    }

    public Task BackAsync(CancellationToken cancellationToken = default)
    {
        if (!CanNavigateBack())
        {
            return Task.CompletedTask;
        }

        return NavigateAsync(GetParentPath(CurrentPath), "正在返回上一级…", cancellationToken);
    }

    public void Dispose()
    {
        CancellationTokenSource? operation;
        lock (_operationGate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            operation = _activeOperation;
        }

        TryCancel(operation);
    }

    private Task NavigateAsync(
        CloudPath path,
        string startStatus,
        CancellationToken cancellationToken) =>
        ExecuteLatestAsync(
            async (generation, token) =>
            {
                await PrepareScanAsync(generation, path);
                var items = await ListAndIndexAsync(path, generation, token);
                token.ThrowIfCancellationRequested();
                await ApplyIfCurrentAsync(generation, () =>
                {
                    CurrentPath = path;
                    SelectedPreviewPath = null;
                    ProgressValue = 1;
                    IsProgressIndeterminate = false;
                    ProgressText = $"扫描完成，共 {items.Count} 项。";
                    StatusText = $"已读取 {path.Value}，共 {items.Count} 项。";
                });
            },
            startStatus,
            cancellationToken);

    private async Task<IReadOnlyList<CloudObject>> ListAndIndexAsync(
        CloudPath path,
        long generation,
        CancellationToken cancellationToken)
    {
        var items = new List<CloudObject>();
        await foreach (var item in _provider.ListAsync(path, cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            items.Add(item);
            cancellationToken.ThrowIfCancellationRequested();
            await ApplyIfCurrentAsync(generation, () =>
            {
                Items.Add(new CloudObjectItemViewModel(item));
                ScannedItemCount++;
                ProgressText = $"已扫描 {ScannedItemCount} 项";
                StatusText = $"正在扫描 {path.Value}：已发现 {ScannedItemCount} 项。";
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _index.UpsertAsync(items, cancellationToken);
        return items;
    }

    private Task PrepareScanAsync(long generation, CloudPath path) =>
        ApplyIfCurrentAsync(generation, () =>
        {
            CurrentPath = path;
            Items.Clear();
            SelectedPreviewPath = null;
            ScannedItemCount = 0;
            ProgressValue = 0;
            IsProgressIndeterminate = true;
            ProgressText = "正在扫描…";
        });

    private async Task ExecuteLatestAsync(
        Func<long, CancellationToken, Task> operation,
        string startStatus,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource? previous;
        CancellationTokenSource current;
        long generation;
        lock (_operationGate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            previous = _activeOperation;
            current = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeOperation = current;
            generation = ++_operationGeneration;
        }

        TryCancel(previous);
        _operationStartedAt = DateTimeOffset.UtcNow;
        try
        {
            await ApplyIfCurrentAsync(generation, () =>
            {
                IsBusy = true;
                ProgressValue = 0;
                ErrorMessage = null;
                StatusText = startStatus;
            });
            await operation(generation, current.Token);
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested)
        {
            await TryApplyOrRecordFailureAsync(generation, () =>
            {
                SelectedPreviewPath = null;
                ProgressText = "操作已取消。";
                StatusText = "操作已取消。";
            });
        }
        catch (Exception exception)
        {
            await TryApplyOrRecordFailureAsync(generation, () =>
            {
                SelectedPreviewPath = null;
                ErrorMessage = exception.Message;
                ProgressText = "操作失败。";
                StatusText = $"操作失败：{exception.Message}";
            }, exception);
        }
        finally
        {
            var ownsActiveOperation = false;
            lock (_operationGate)
            {
                if (ReferenceEquals(_activeOperation, current))
                {
                    _activeOperation = null;
                    ownsActiveOperation = true;
                }
            }

            if (ownsActiveOperation)
            {
                await TryApplyOrRecordFailureAsync(generation, () =>
                {
                    IsBusy = false;
                    IsProgressIndeterminate = false;
                });
            }

            current.Dispose();
        }
    }

    private async Task TryApplyOrRecordFailureAsync(
        long generation,
        Action action,
        Exception? originalException = null)
    {
        try
        {
            await ApplyIfCurrentAsync(generation, action);
        }
        catch (Exception dispatchException)
        {
            if (Interlocked.Read(ref _operationGeneration) != generation)
            {
                return;
            }

            var failure = originalException ?? dispatchException;
            _selectedPreviewPath = null;
            _errorMessage = failure.Message;
            _statusText = $"操作失败：{failure.Message}";
            _progressText = "操作失败。";
            _isBusy = false;
            _isProgressIndeterminate = false;
        }
    }

    private void CancelCurrentOperation()
    {
        CancellationTokenSource? operation;
        lock (_operationGate)
        {
            operation = _activeOperation;
        }

        TryCancel(operation);
    }

    private bool CanNavigateBack() => CurrentPath.Value != "/";

    private async Task ApplyIfCurrentAsync(long generation, Action action)
    {
        await InvokeOnCapturedContextAsync(() =>
        {
            if (Interlocked.Read(ref _operationGeneration) == generation)
            {
                action();
            }
        });
    }

    private Task InvokeOnCapturedContextAsync(Action action)
    {
        if (ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _synchronizationContext.Post(
            static state =>
            {
                var (callback, source) = ((Action, TaskCompletionSource))state!;
                try
                {
                    callback();
                    source.SetResult();
                }
                catch (Exception exception)
                {
                    source.SetException(exception);
                }
            },
            (action, completion));
        return completion.Task;
    }

    private static void TryCancel(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A just-completed operation can dispose its source while a newer one supersedes it.
        }
    }

    private static string CreateThumbnailCacheKey(
        CloudProviderKind provider,
        string remoteId) => $"{ThumbnailCachePrefix}:{(int)provider}:{remoteId}";

    private static bool IsReadableCacheFile(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return stream.CanRead;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static CloudPath GetParentPath(CloudPath path)
    {
        var lastSeparator = path.Value.LastIndexOf('/');
        return lastSeparator <= 0
            ? new CloudPath("/")
            : new CloudPath(path.Value[..lastSeparator]);
    }

    private static string FormatBytes(long bytes)
    {
        const double mb = 1024.0 * 1024.0;
        const double gb = mb * 1024.0;
        const double tb = gb * 1024.0;
        return bytes >= tb
            ? $"{bytes / tb:0.##} TB"
            : bytes >= gb
                ? $"{bytes / gb:0.#} GB"
                : bytes >= mb
                    ? $"{bytes / mb:0.#} MB"
                    : $"{bytes} B";
    }
}

public sealed class CloudObjectItemViewModel : ObservableObject
{
    public CloudObjectItemViewModel(CloudObject source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public CloudObject Source { get; }

    public CloudProviderKind Provider => Source.Provider;

    public string RemoteId => Source.RemoteId;

    public CloudPath Path => Source.Path;

    public string Name => Source.Name;

    public CloudObjectKind Kind => Source.Kind;

    public long Size => Source.Size;

    public DateTimeOffset ModifiedAt => Source.ModifiedAt;

    public string? ThumbnailKey => Source.ThumbnailKey;

    public bool IsHanabeManaged => Source.IsHanabeManaged;

    public bool IsFolder => Kind == CloudObjectKind.Folder;
}
