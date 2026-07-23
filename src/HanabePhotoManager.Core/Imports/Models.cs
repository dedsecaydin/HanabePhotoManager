namespace HanabePhotoManager.Core.Imports;

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

public enum TransferMode
{
    CopyKeepSource,
    CopyThenAskDelete,
    MoveAfterVerify
}

public enum ConflictKind
{
    None,
    Identical,
    SameNameDifferentContent
}

public enum ImportItemState
{
    Planned,
    Copying,
    Verifying,
    Completed,
    Skipped,
    Failed
}

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

public sealed record SourceMediaFile(string FullPath, long Length, DateTimeOffset LastWriteTime);

public sealed record MediaGroup(
    string GroupKey,
    MediaCategory Category,
    SourceMediaFile Primary,
    IReadOnlyList<SourceMediaFile> Sidecars)
{
    public IReadOnlyList<SourceMediaFile> Sidecars { get; } = Array.AsReadOnly(Sidecars.ToArray());
}

public sealed record ImportCandidate(
    SourceMediaFile File,
    MediaCategory SuggestedCategory,
    string Rule,
    bool RequiresConfirmation);

public sealed record PlannedFile(
    SourceMediaFile Source,
    string DestinationPath,
    string TemporaryPath,
    ConflictKind Conflict);

public sealed record ImportPlanItem(
    Guid Id,
    MediaGroup Group,
    IReadOnlyList<PlannedFile> Files,
    ConflictKind Conflict,
    ImportItemState State)
{
    public IReadOnlyList<PlannedFile> Files { get; } = Array.AsReadOnly(Files.ToArray());
}

public sealed record ImportPlan(
    string LibraryRoot,
    LibraryDate Date,
    TransferMode Mode,
    IReadOnlyList<ImportPlanItem> Items)
{
    public IReadOnlyList<ImportPlanItem> Items { get; } = Array.AsReadOnly(Items.ToArray());
}
