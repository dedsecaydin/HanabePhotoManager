using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using SendKeys = System.Windows.Forms.SendKeys;

namespace HanabePhotoManager.App.WeChat;

public sealed class WindowsWeChatDesktopGateway : IWeChatDesktopGateway
{
    private static readonly string[] ProcessNames = ["Weixin", "WeChat"];
    private readonly WeChatExecutableLocator _locator;
    private readonly string? _configuredPath;
    private Process? _process;
    private nint _windowHandle;
    private WeChatTarget? _target;

    public WindowsWeChatDesktopGateway(
        WeChatExecutableLocator? locator = null,
        string? configuredPath = null)
    {
        _locator = locator ?? new WeChatExecutableLocator();
        _configuredPath = configuredPath;
    }

    public async Task<WeChatGatewayStatus> EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return new(false, false, "微信桌面自动发送仅支持 Windows。");

        _process = FindRunningProcess();
        if (_process is null)
        {
            var executable = _locator.Locate(_configuredPath);
            if (executable is null)
                return new(true, false, "未找到微信程序，请在设置中选择 Weixin.exe 或 WeChat.exe。");

            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _process = FindRunningProcess();
            if (_process is { HasExited: false } && TryGetWindow(_process, out _windowHandle))
            {
                if (TryBringToFront(_process, _windowHandle))
                    return new(true, true, "微信已在前台。", _process.Id);
            }
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        return new(true, false, "微信启动超时，可能仍在登录或更新。");
    }

    public async Task<WeChatTarget?> LocateTargetAsync(
        string requestedName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedName))
            return null;

        var ready = await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        if (!ready.IsReady || _process is null || !TryBringToFront(_process, _windowHandle))
            return null;

        return await OnUiThreadAsync(() =>
        {
            var root = AutomationElement.FromHandle(_windowHandle);
            var search = FindFirst(root, ControlType.Edit, ["搜索", "Search"]);
            if (search is null || !search.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern))
                return null;

            ((ValuePattern)valuePattern).SetValue(requestedName.Trim());
            Thread.Sleep(300);
            var matches = root.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, requestedName.Trim()))
                .Cast<AutomationElement>()
                .Where(element => element.Current.IsEnabled)
                .ToArray();
            if (matches.Length != 1)
                return null;

            if (matches[0].TryGetCurrentPattern(InvokePattern.Pattern, out var invokePattern))
                ((InvokePattern)invokePattern).Invoke();
            else if (matches[0].TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionPattern))
                ((SelectionItemPattern)selectionPattern).Select();
            else
                return null;

            Thread.Sleep(250);
            if (!HasNamedElement(root, requestedName.Trim()))
                return null;

            _target = new(
                requestedName.Trim(),
                requestedName.Trim(),
                requestedName.Trim() == "文件传输助手" ? "文件传输助手" : "联系人或群聊",
                $"{_process.Id}:{_process.StartTime.ToUniversalTime().Ticks}:{Guid.NewGuid():N}");
            return _target;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WeChatBatchSendResult> SendBatchAsync(
        IReadOnlyList<WeChatSendItem> items,
        WeChatTarget target,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || _process is null || _target != target)
            return Ambiguous(items, "微信或发送目标未准备好。");
        if (!TryBringToFront(_process, _windowHandle))
            return Ambiguous(items, "微信不在前台，已暂停。");
        if (items.Any(item => !SnapshotMatches(item)))
            return Ambiguous(items, "原文件已变化，未发送。");

        return await OnUiThreadAsync(() =>
        {
            var root = AutomationElement.FromHandle(_windowHandle);
            if (!HasNamedElement(root, target.ResolvedTitle))
                return Ambiguous(items, "当前聊天目标与已确认目标不一致。");

            var edit = FindMessageInput(root);
            if (edit is null || !IsInputEmpty(edit))
                return Ambiguous(items, "微信输入框中已有文字、附件或草稿。");

            var beforeCounts = items.ToDictionary(
                item => item.QueueItemId,
                item => CountNamedElements(root, item.DisplayName));
            var fileList = new StringCollection();
            fileList.AddRange(items.Select(item => item.SourcePath).ToArray());
            System.Windows.Clipboard.SetFileDropList(fileList);

            if (!TryBringToFront(_process, _windowHandle))
                return Ambiguous(items, "投递前微信失去前台。");
            edit.SetFocus();
            SendKeys.SendWait("^v");
            Thread.Sleep(500);

            if (items.Any(item => CountNamedElements(root, item.DisplayName) <= beforeCounts[item.QueueItemId]))
                return Ambiguous(items, "附件预览数量或文件名无法确认，未按发送键。");
            if (!TryBringToFront(_process, _windowHandle) || !HasNamedElement(root, target.ResolvedTitle))
                return Ambiguous(items, "发送前微信窗口或目标发生变化。");

            SendKeys.SendWait("{ENTER}");
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(250);
                if (!TryBringToFront(_process, _windowHandle) || !HasNamedElement(root, target.ResolvedTitle))
                    return Ambiguous(items, "验证期间微信窗口或目标发生变化。");
                if (items.All(item => CountNamedElements(root, item.DisplayName) > beforeCounts[item.QueueItemId]))
                {
                    var hasFailure = HasNamedElement(root, "发送失败") || HasNamedElement(root, "重试");
                    return new(items.Select(item => new WeChatItemEvidence(
                        item.QueueItemId,
                        hasFailure ? WeChatEvidenceState.Failed : WeChatEvidenceState.Sent,
                        InputCleared: IsInputEmpty(edit),
                        NewFileBubbleFound: true,
                        UploadCompleted: !hasFailure,
                        FailureMarkerFound: hasFailure,
                        TargetUnchanged: true,
                        hasFailure ? "微信显示发送失败。" : "已验证新增文件消息。")).ToArray());
                }
            }

            return Ambiguous(items, "结果验证超时，未自动重试。");
        }, cancellationToken).ConfigureAwait(false);
    }

    private static Process? FindRunningProcess() =>
        ProcessNames.SelectMany(Process.GetProcessesByName)
            .Where(process =>
            {
                try { return !process.HasExited; }
                catch { return false; }
            })
            .OrderByDescending(process => process.MainWindowHandle != nint.Zero)
            .FirstOrDefault();

    private static bool TryGetWindow(Process process, out nint handle)
    {
        process.Refresh();
        handle = process.MainWindowHandle;
        return handle != nint.Zero;
    }

    private static bool TryBringToFront(Process process, nint handle)
    {
        if (handle == nint.Zero || process.HasExited)
            return false;
        WeChatNativeMethods.ShowWindow(handle, WeChatNativeMethods.SwRestore);
        WeChatNativeMethods.SetForegroundWindow(handle);
        var foreground = WeChatNativeMethods.GetForegroundWindow();
        WeChatNativeMethods.GetWindowThreadProcessId(foreground, out var pid);
        return foreground == handle
               && WeChatForegroundVerifier.IsVerifiedForeground((int)pid, [process.Id]);
    }

    private static bool SnapshotMatches(WeChatSendItem item)
    {
        var file = new FileInfo(item.SourcePath);
        return file.Exists
               && file.Length == item.Length
               && file.LastWriteTimeUtc == item.LastWriteTime.UtcDateTime;
    }

    private static AutomationElement? FindMessageInput(AutomationElement root) =>
        root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit))
            .Cast<AutomationElement>()
            .LastOrDefault(element => element.Current.IsEnabled && !element.Current.IsOffscreen);

    private static AutomationElement? FindFirst(
        AutomationElement root,
        ControlType type,
        IReadOnlyList<string> names) =>
        root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, type))
            .Cast<AutomationElement>()
            .FirstOrDefault(element => names.Any(name =>
                element.Current.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));

    private static bool IsInputEmpty(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern))
            return string.IsNullOrWhiteSpace(((ValuePattern)pattern).Current.Value);
        return string.IsNullOrWhiteSpace(element.Current.Name);
    }

    private static bool HasNamedElement(AutomationElement root, string name) =>
        CountNamedElements(root, name) > 0;

    private static int CountNamedElements(AutomationElement root, string name) =>
        root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, name))
            .Count;

    private static WeChatBatchSendResult Ambiguous(
        IEnumerable<WeChatSendItem> items,
        string message) =>
        new(items.Select(item => new WeChatItemEvidence(
            item.QueueItemId,
            WeChatEvidenceState.Ambiguous,
            false,
            false,
            false,
            false,
            false,
            message)).ToArray());

    private static Task<T> OnUiThreadAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            return Task.FromResult(action());
        return dispatcher.InvokeAsync(action, System.Windows.Threading.DispatcherPriority.Normal, cancellationToken)
            .Task;
    }
}
