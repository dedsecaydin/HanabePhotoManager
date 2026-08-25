namespace HanabePhotoManager.App.Duplicates;

/// <summary>单个导入重复项的处理决定。</summary>
public enum ImportDuplicateDecision
{
    Skip,
    ImportAnyway,
    LocateExisting
}

/// <summary>根据精确重复和只读修后状态决定允许的单项操作。</summary>
public static class ImportDuplicateDecisionPolicy
{
    public static bool ShouldTransfer(ImportDuplicateDecision decision) =>
        decision == ImportDuplicateDecision.ImportAnyway;
}
