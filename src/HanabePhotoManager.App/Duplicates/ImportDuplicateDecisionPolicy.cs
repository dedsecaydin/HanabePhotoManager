namespace HanabePhotoManager.App.Duplicates;

public enum ImportDuplicateDecision
{
    Skip,
    ImportAnyway,
    LocateExisting
}

public static class ImportDuplicateDecisionPolicy
{
    public static bool ShouldTransfer(ImportDuplicateDecision decision) =>
        decision == ImportDuplicateDecision.ImportAnyway;
}
