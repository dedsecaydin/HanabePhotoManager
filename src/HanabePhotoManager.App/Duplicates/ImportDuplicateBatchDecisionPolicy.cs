namespace HanabePhotoManager.App.Duplicates;

public enum ImportDuplicateBatchDecision
{
    SkipAll,
    ImportAll,
    DecideIndividually
}

public static class ImportDuplicateBatchDecisionPolicy
{
    public static bool ShouldTransfer(ImportDuplicateBatchDecision decision) =>
        decision == ImportDuplicateBatchDecision.ImportAll;

    public static bool ShouldPromptIndividually(ImportDuplicateBatchDecision decision) =>
        decision == ImportDuplicateBatchDecision.DecideIndividually;
}

public sealed record ImportDuplicateMatch(string IncomingPath, string ExistingPath, bool ExistingIsReadOnlyRetouched);
