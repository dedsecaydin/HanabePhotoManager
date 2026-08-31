namespace HanabePhotoManager.App.Services;

public enum HanabeAssistantState
{
    Idle, Scanning, Checking, Importing, StopRequested, Canceled, Interrupted,
    Recoverable, Resuming, Completed, CompletedWithIssues, Failed, Error
}

public enum HanabeAssistantVisualStyle
{
    ChibiAnimated,
    PixelAnimated,
    OldMoneyAnimated,
    RainyAnimated,
    WinterAnimated,
    Static,
    Off
}

internal static class HanabeAssistantAnimationResolver
{
    internal static string Resolve(HanabeAssistantVisualStyle style, HanabeAssistantState state)
    {
        if (style is HanabeAssistantVisualStyle.Static or HanabeAssistantVisualStyle.Off) return "Assets/Hanabe/hanabe-assistant.png";
        var animation = state switch
        {
            HanabeAssistantState.StopRequested or HanabeAssistantState.Canceled or HanabeAssistantState.Recoverable => "idle",
            HanabeAssistantState.Interrupted or HanabeAssistantState.Failed or HanabeAssistantState.Error => "error",
            HanabeAssistantState.Resuming => "importing",
            HanabeAssistantState.CompletedWithIssues => "completed",
            _ => state.ToString().ToLowerInvariant()
        };
        var folder = style switch
        {
            HanabeAssistantVisualStyle.PixelAnimated => "Pixel",
            HanabeAssistantVisualStyle.OldMoneyAnimated => "OldMoney",
            HanabeAssistantVisualStyle.RainyAnimated => "Rainy",
            HanabeAssistantVisualStyle.WinterAnimated => "Winter",
            _ => "Chibi"
        };
        return $"Assets/Hanabe/Animated/{folder}/{animation}.gif";
    }
}

public sealed record HanabeAssistantSnapshot(
    HanabeAssistantState State, string StatusLabel, string TaskTitle, string Detail, string ResultHint,
    string Summary, bool IsProgressVisible, bool IsProgressIndeterminate, double ProgressValue,
    bool CanStop, bool CanResume, bool CanViewReport, bool IsPersistent)
{
    public string AccessibleName => string.Join("，", new[] { StatusLabel, TaskTitle, Detail, ResultHint }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

internal static class HanabeAssistantPresentationPolicy
{
    internal static HanabeAssistantSnapshot Create(HanabeAssistantState state, string detail, double progress,
        bool indeterminate, int success, int skipped, int failed, bool hasRecoveryPoint = false)
    {
        var (status, task, hint) = state switch
        {
            HanabeAssistantState.Scanning => ("扫描", "正在扫描媒体", "正在建立安全的导入清单"),
            HanabeAssistantState.Checking => ("校验", "正在校验重复内容", "文件信息筛选后进行 SHA-256 校验"),
            HanabeAssistantState.Importing => ("传输中", "正在安全传输", "每个文件完成后更新安全恢复点"),
            HanabeAssistantState.StopRequested => ("正在停止", "正在安全停止…", "将在当前文件安全点停止，请稍候"),
            HanabeAssistantState.Canceled => ("已停止", "任务已停止", "已完成内容已保留，未完成内容可稍后继续"),
            HanabeAssistantState.Interrupted => ("意外中断", "任务意外中断", hasRecoveryPoint ? "恢复点已保留，可打开 Hanabe 继续" : "没有可用恢复点，请查看原因"),
            HanabeAssistantState.Recoverable => ("等待恢复", "可继续上次导入", "已完成内容不会重复传输"),
            HanabeAssistantState.Resuming => ("恢复中", "正在恢复上次导入", "正在核对恢复点并继续安全传输"),
            HanabeAssistantState.Completed => ("完成", "任务完成", "全部处理已安全结束"),
            HanabeAssistantState.CompletedWithIssues => ("需要处理", "已完成，但有项目需处理", "打开报告查看失败或冲突项目"),
            HanabeAssistantState.Failed or HanabeAssistantState.Error => ("失败", "任务失败", "打开 Hanabe 查看原因并重试"),
            _ => ("空闲", "Hanabe 正在待命", "主窗口最小化后，我会在这里显示后台任务")
        };
        var running = state is HanabeAssistantState.Scanning or HanabeAssistantState.Checking or HanabeAssistantState.Importing or HanabeAssistantState.Resuming;
        var summary = success + skipped + failed > 0 ? $"成功 {success:N0}  ·  跳过 {skipped:N0}  ·  失败 {failed:N0}" : string.Empty;
        return new(state, status, task, detail, hint, summary, running || state == HanabeAssistantState.StopRequested,
            running && indeterminate, Math.Clamp(progress, 0, 100), running,
            state is HanabeAssistantState.Canceled or HanabeAssistantState.Recoverable || state == HanabeAssistantState.Interrupted && hasRecoveryPoint,
            state is HanabeAssistantState.Completed or HanabeAssistantState.CompletedWithIssues or HanabeAssistantState.Interrupted or HanabeAssistantState.Failed or HanabeAssistantState.Error,
            state is HanabeAssistantState.Canceled or HanabeAssistantState.Interrupted or HanabeAssistantState.Recoverable or HanabeAssistantState.CompletedWithIssues or HanabeAssistantState.Failed or HanabeAssistantState.Error);
    }
}
