using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Compression;

namespace HanabePhotoManager.App.WeChat;

public sealed class WeChatSenderViewModel : ObservableObject
{
    private readonly IWeChatDesktopGateway _gateway;
    private readonly WeChatSendQueueService _queueService;
    private readonly ImageInputDiscovery _discovery;
    private readonly IWeChatFileClipboard _clipboard;
    private readonly List<Guid> _manualBatchIds = [];
    private CancellationTokenSource? _cancellation;
    private string _targetName = string.Empty;
    private WeChatTarget? _locatedTarget;
    private WeChatTarget? _confirmedTarget;
    private string _statusText = "拖入原图后，先检测微信并确认发送目标。";
    private bool _isReady;
    private bool _isRunning;
    private int _sentCount;
    private int _failedCount;
    private int _ambiguousCount;
    private int _currentBatch;
    private string _currentFile = string.Empty;
    private bool _isManualFallbackAvailable;

    public WeChatSenderViewModel(
        IWeChatDesktopGateway? gateway = null,
        ImageInputDiscovery? discovery = null,
        IWeChatFileClipboard? clipboard = null)
    {
        _gateway = gateway ?? new WindowsWeChatDesktopGateway();
        _queueService = new WeChatSendQueueService(_gateway, log: new JsonWeChatSendLog());
        _discovery = discovery ?? new ImageInputDiscovery();
        _clipboard = clipboard ?? new WindowsWeChatFileClipboard();
        DetectCommand = new AsyncRelayCommand(DetectAsync, () => !IsRunning);
        LocateTargetCommand = new AsyncRelayCommand(LocateTargetAsync, CanLocateTarget);
        ConfirmTargetCommand = new RelayCommand(ConfirmTarget, CanConfirmTarget);
        StartCommand = new AsyncRelayCommand(StartAsync, CanStart);
        PrepareManualBatchCommand = new AsyncRelayCommand(
            PrepareManualBatchAsync,
            CanPrepareManualBatch);
        ConfirmManualBatchSentCommand = new RelayCommand(
            ConfirmManualBatchSent,
            () => HasPreparedManualBatch && !IsRunning);
        CancelCommand = new RelayCommand(() => _cancellation?.Cancel(), () => IsRunning);
        ClearCommand = new RelayCommand(Clear, () => !IsRunning && Items.Count > 0);
        RemoveCommand = new RelayCommand<WeChatSendItem>(
            Remove,
            item => item is not null && !IsRunning && !HasPreparedManualBatch);
    }

    public ObservableCollection<WeChatSendItem> Items { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    public IAsyncRelayCommand DetectCommand { get; }
    public IAsyncRelayCommand LocateTargetCommand { get; }
    public IRelayCommand ConfirmTargetCommand { get; }
    public IAsyncRelayCommand StartCommand { get; }
    public IAsyncRelayCommand PrepareManualBatchCommand { get; }
    public IRelayCommand ConfirmManualBatchSentCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand<WeChatSendItem> RemoveCommand { get; }

    public string TargetName
    {
        get => _targetName;
        set
        {
            if (!SetProperty(ref _targetName, value ?? string.Empty))
                return;
            _locatedTarget = null;
            _confirmedTarget = null;
            _manualBatchIds.Clear();
            IsManualFallbackAvailable = false;
            OnPropertyChanged(nameof(LocatedTitle));
            OnPropertyChanged(nameof(LocatedTargetType));
            OnPropertyChanged(nameof(IsTargetConfirmed));
            NotifyCommands();
        }
    }

    public string LocatedTitle => _locatedTarget?.ResolvedTitle ?? string.Empty;
    public string LocatedTargetType => _locatedTarget?.TargetType ?? string.Empty;
    public bool IsTargetConfirmed => _confirmedTarget is not null;
    public bool IsManualFallbackAvailable
    {
        get => _isManualFallbackAvailable;
        private set
        {
            if (SetProperty(ref _isManualFallbackAvailable, value))
            {
                NotifyCommands();
            }
        }
    }
    public bool HasPreparedManualBatch => _manualBatchIds.Count > 0;
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsReady { get => _isReady; private set { if (SetProperty(ref _isReady, value)) NotifyCommands(); } }
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetProperty(ref _isRunning, value))
                return;
            NotifyCommands();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }
    public int SentCount { get => _sentCount; private set => SetProperty(ref _sentCount, value); }
    public int FailedCount { get => _failedCount; private set => SetProperty(ref _failedCount, value); }
    public int AmbiguousCount { get => _ambiguousCount; private set => SetProperty(ref _ambiguousCount, value); }
    public int CurrentBatch { get => _currentBatch; private set => SetProperty(ref _currentBatch, value); }
    public string CurrentFile { get => _currentFile; private set => SetProperty(ref _currentFile, value); }
    public double ProgressValue => Items.Count == 0 ? 0 : SentCount * 100d / Items.Count;

    public void AddInputs(IEnumerable<string> paths, bool recursive = true)
    {
        var result = _discovery.Discover(paths, recursive, CancellationToken.None);
        var existing = Items.Select(item => item.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in result.Files.Where(existing.Add))
        {
            var file = new FileInfo(path);
            Items.Add(WeChatSendItem.Create(path, file.Length, file.LastWriteTimeUtc));
        }
        foreach (var warning in result.Warnings)
            Warnings.Add(warning);
        StatusText = $"已选择 {Items.Count:N0} 张原图；发送前不会压缩或复制。";
        OnPropertyChanged(nameof(ProgressValue));
        NotifyCommands();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private async Task DetectAsync()
    {
        var status = await _gateway.EnsureReadyAsync(CancellationToken.None);
        IsReady = status.IsSupported && status.IsReady;
        StatusText = status.Message;
    }

    private bool CanLocateTarget() =>
        !IsRunning && IsReady && !string.IsNullOrWhiteSpace(TargetName);

    private async Task LocateTargetAsync()
    {
        _confirmedTarget = null;
        _locatedTarget = await _gateway.LocateTargetAsync(TargetName, CancellationToken.None);
        OnPropertyChanged(nameof(LocatedTitle));
        OnPropertyChanged(nameof(LocatedTargetType));
        OnPropertyChanged(nameof(IsTargetConfirmed));
        IsManualFallbackAvailable = _locatedTarget is null;
        StatusText = _locatedTarget is null
            ? $"微信新版界面无法安全读取目标。请在微信手动打开“{TargetName.Trim()}”，再使用下方手动批次模式。"
            : $"请核对微信当前聊天：{LocatedTitle}（{LocatedTargetType}）。";
        NotifyCommands();
    }

    private bool CanConfirmTarget() => !IsRunning && _locatedTarget is not null;

    private void ConfirmTarget()
    {
        _confirmedTarget = _locatedTarget;
        OnPropertyChanged(nameof(IsTargetConfirmed));
        StatusText = $"已确认发送目标：{LocatedTitle}。";
        NotifyCommands();
    }

    private bool CanStart() =>
        !IsRunning && IsReady && Items.Count > 0 && _confirmedTarget is not null;

    private bool CanPrepareManualBatch() =>
        !IsRunning
        && IsReady
        && IsManualFallbackAvailable
        && !string.IsNullOrWhiteSpace(TargetName)
        && (HasPreparedManualBatch || Items.Any(item =>
            item.State is WeChatSendItemState.Pending or WeChatSendItemState.Failed));

    private async Task PrepareManualBatchAsync()
    {
        var status = await _gateway.EnsureReadyAsync(CancellationToken.None);
        if (!status.IsReady)
        {
            StatusText = status.Message;
            return;
        }

        var batch = HasPreparedManualBatch
            ? Items.Where(item => _manualBatchIds.Contains(item.QueueItemId)).ToArray()
            : Items.Where(item =>
                    item.State is WeChatSendItemState.Pending or WeChatSendItemState.Failed)
                .Take(9)
                .ToArray();
        if (batch.Length == 0)
        {
            StatusText = "没有待发送照片。";
            return;
        }

        var valid = batch.Where(SnapshotMatches).ToArray();
        foreach (var changed in batch.Except(valid))
        {
            ReplaceItem(changed with
            {
                State = WeChatSendItemState.Changed,
                Message = "原文件已变化，请移除后重新添加。"
            });
        }
        if (valid.Length == 0)
        {
            StatusText = "本批文件均已变化，未复制。";
            NotifyCommands();
            return;
        }

        try
        {
            _clipboard.SetFiles(valid.Select(item => item.SourcePath).ToArray());
        }
        catch (Exception ex)
        {
            StatusText = $"复制文件失败：{ex.Message}";
            return;
        }

        if (!HasPreparedManualBatch)
        {
            CurrentBatch++;
        }
        _manualBatchIds.Clear();
        _manualBatchIds.AddRange(valid.Select(item => item.QueueItemId));
        foreach (var item in valid)
        {
            ReplaceItem(item with
            {
                State = WeChatSendItemState.ReadyToSend,
                Message = "已复制到文件剪贴板，等待你在微信粘贴发送。"
            });
        }

        OnPropertyChanged(nameof(HasPreparedManualBatch));
        StatusText =
            $"已复制本批 {valid.Length} 张原图。请确认微信当前是“{TargetName.Trim()}”，按 Ctrl+V 后发送；完成后回到这里点击“本批已发送”。";
        NotifyCommands();
    }

    private void ConfirmManualBatchSent()
    {
        if (!HasPreparedManualBatch || IsRunning)
        {
            return;
        }

        foreach (var id in _manualBatchIds.ToArray())
        {
            var item = Items.FirstOrDefault(candidate => candidate.QueueItemId == id);
            if (item is not null)
            {
                ReplaceItem(item with
                {
                    State = WeChatSendItemState.Sent,
                    Message = "用户已确认本批在微信中发送完成。"
                });
            }
        }

        _manualBatchIds.Clear();
        SentCount = Items.Count(item => item.State == WeChatSendItemState.Sent);
        OnPropertyChanged(nameof(HasPreparedManualBatch));
        OnPropertyChanged(nameof(ProgressValue));
        StatusText = Items.Any(item =>
            item.State is WeChatSendItemState.Pending or WeChatSendItemState.Failed)
            ? "本批已记录完成。请点击“复制下一批”继续。"
            : $"手动发送完成：{SentCount} 张。";
        NotifyCommands();
    }

    private async Task StartAsync()
    {
        if (!CanStart() || _confirmedTarget is null)
            return;

        _cancellation = new CancellationTokenSource();
        IsRunning = true;
        var progress = new Progress<WeChatQueueProgress>(report =>
        {
            SentCount = report.Sent;
            FailedCount = report.Failed;
            AmbiguousCount = report.Ambiguous;
            CurrentBatch = report.CurrentBatch;
            CurrentFile = report.CurrentFile;
            OnPropertyChanged(nameof(ProgressValue));
        });

        try
        {
            var result = await _queueService.SendAsync(
                Items.ToArray(), _confirmedTarget, progress, _cancellation.Token);
            Items.Clear();
            foreach (var item in result.Items)
                Items.Add(item);
            SentCount = Items.Count(item => item.State == WeChatSendItemState.Sent);
            FailedCount = Items.Count(item => item.State == WeChatSendItemState.Failed);
            AmbiguousCount = Items.Count(item => item.State == WeChatSendItemState.Ambiguous);
            OnPropertyChanged(nameof(ProgressValue));
            StatusText = result.IsCanceled
                ? "已取消；已交给微信的内容无法撤回，请核对微信输入框。"
                : result.IsPaused
                    ? "结果不确定，已暂停且不会自动重试。"
                    : $"完成：{SentCount} 成功，{FailedCount} 失败。";
        }
        finally
        {
            CurrentFile = string.Empty;
            IsRunning = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    private void Remove(WeChatSendItem? item)
    {
        if (item is null || IsRunning)
            return;
        Items.Remove(item);
        NotifyCommands();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private void Clear()
    {
        Items.Clear();
        Warnings.Clear();
        SentCount = FailedCount = AmbiguousCount = CurrentBatch = 0;
        _manualBatchIds.Clear();
        IsManualFallbackAvailable = false;
        OnPropertyChanged(nameof(HasPreparedManualBatch));
        StatusText = "拖入原图后，先检测微信并确认发送目标。";
        OnPropertyChanged(nameof(ProgressValue));
        NotifyCommands();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCommands()
    {
        DetectCommand.NotifyCanExecuteChanged();
        LocateTargetCommand.NotifyCanExecuteChanged();
        ConfirmTargetCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
        PrepareManualBatchCommand.NotifyCanExecuteChanged();
        ConfirmManualBatchSentCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    private static bool SnapshotMatches(WeChatSendItem item)
    {
        var file = new FileInfo(item.SourcePath);
        return file.Exists
               && file.Length == item.Length
               && file.LastWriteTimeUtc == item.LastWriteTime.UtcDateTime;
    }

    private void ReplaceItem(WeChatSendItem replacement)
    {
        var index = Items
            .Select((item, itemIndex) => (item, itemIndex))
            .FirstOrDefault(pair => pair.item.QueueItemId == replacement.QueueItemId)
            .itemIndex;
        if (index >= 0 && index < Items.Count &&
            Items[index].QueueItemId == replacement.QueueItemId)
        {
            Items[index] = replacement;
        }
    }
}
