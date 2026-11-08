namespace HanabePhotoManager.App.Duplicates;

/// <summary>重复项批量跳过、保留或逐项选择策略。</summary>
public enum ImportDuplicateBatchDecision
{
    SkipAll,
    ImportAll,
    DecideIndividually
}

/// <summary>把批量重复处理选项应用到每个导入匹配项。</summary>
public static class ImportDuplicateBatchDecisionPolicy
{
    public static bool ShouldTransfer(ImportDuplicateBatchDecision decision) =>
        decision == ImportDuplicateBatchDecision.ImportAll;

    public static bool ShouldPromptIndividually(ImportDuplicateBatchDecision decision) =>
        decision == ImportDuplicateBatchDecision.DecideIndividually;
}

/// <summary>导入文件与照片库现有文件之间的重复匹配。</summary>
public sealed record ImportDuplicateMatch(string IncomingPath, string ExistingPath, bool ExistingIsReadOnlyRetouched);
