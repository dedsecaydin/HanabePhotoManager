namespace HanabePhotoManager.Core.Imports;

/// <summary>导入阶段使用的媒体类别，并决定目标分类目录。</summary>
public enum MediaCategory
{
    Raw,
    Jpeg,
    Edited,
    Video,
    ActionVideo,
    Material,
    Unconfirmed
}

/// <summary>源文件在验证成功后的保留或删除策略。</summary>
public enum TransferMode
{
    CopyKeepSource,
    CopyThenAskDelete,
    MoveAfterVerify
}

/// <summary>目标位置与源文件之间的冲突判定。</summary>
public enum ConflictKind
{
    None,
    Identical,
    SameNameDifferentContent
}

/// <summary>单个导入计划项在执行过程中的状态。</summary>
public enum ImportItemState
{
    Planned,
    Copying,
    Verifying,
    Completed,
    Skipped,
    Failed
}

/// <summary>
/// 照片库使用的自然日值，并提供与现有月/日目录约定一致的相对路径。
/// </summary>
public readonly record struct LibraryDate
{
    private readonly DateOnly _value;

    public LibraryDate(int year, int month, int day)
    {
        _value = new DateOnly(year, month, day);
    }

    public int Year => _value.Year;

    public int Month => _value.Month;

    public int Day => _value.Day;

    public string RelativePath => Path.Combine($"{Month}月", $"{Month:00}.{Day:00}");
}

/// <summary>导入源文件在分析时取得的稳定路径、大小和修改时间快照。</summary>
public sealed record SourceMediaFile(string FullPath, long Length, DateTimeOffset LastWriteTime);

/// <summary>一个主媒体文件及随其一起传输的 XML、LRF 或音频附属文件。</summary>
public sealed record MediaGroup(
    string GroupKey,
    MediaCategory Category,
    SourceMediaFile Primary,
    IReadOnlyList<SourceMediaFile> Sidecars)
{
    public IReadOnlyList<SourceMediaFile> Sidecars { get; } = Array.AsReadOnly(Sidecars.ToArray());
}

/// <summary>媒体分类器对单个源文件给出的建议及确认要求。</summary>
public sealed record ImportCandidate(
    SourceMediaFile File,
    MediaCategory SuggestedCategory,
    string Rule,
    bool RequiresConfirmation);

/// <summary>一个源文件对应的最终路径、临时路径和冲突状态。</summary>
public sealed record PlannedFile(
    SourceMediaFile Source,
    string DestinationPath,
    string TemporaryPath,
    ConflictKind Conflict);

/// <summary>可独立执行和追踪状态的媒体组导入计划项。</summary>
public sealed record ImportPlanItem(
    Guid Id,
    MediaGroup Group,
    IReadOnlyList<PlannedFile> Files,
    ConflictKind Conflict,
    ImportItemState State)
{
    public IReadOnlyList<PlannedFile> Files { get; } = Array.AsReadOnly(Files.ToArray());
}

/// <summary>一次导入任务的根目录、日期、传输模式和全部计划项。</summary>
public sealed record ImportPlan(
    string LibraryRoot,
    LibraryDate Date,
    TransferMode Mode,
    IReadOnlyList<ImportPlanItem> Items)
{
    public IReadOnlyList<ImportPlanItem> Items { get; } = Array.AsReadOnly(Items.ToArray());
}
