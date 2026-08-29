namespace HanabePhotoManager.App.Duplicates;

public enum DuplicateMergeScope
{
    ExactSha256Only,
    VisualSimilarOnly,
    ExactAndVisual,
}

public static class DuplicateMergeScopePolicy
{
    public static bool IncludesExact(DuplicateMergeScope scope) =>
        scope is DuplicateMergeScope.ExactSha256Only or DuplicateMergeScope.ExactAndVisual;

    public static bool IncludesVisual(DuplicateMergeScope scope) =>
        scope is DuplicateMergeScope.VisualSimilarOnly or DuplicateMergeScope.ExactAndVisual;
}
