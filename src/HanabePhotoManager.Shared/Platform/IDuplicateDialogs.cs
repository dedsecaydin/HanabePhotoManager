using HanabePhotoManager.App.Duplicates;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.App.Platform;

public interface IDuplicateDialogs
{
    DuplicateMergeScope? ChooseScope();
    HashSet<string>? Review(List<DuplicateCandidateGroup> candidates, string root, LibraryContentScanner scanner, bool showDifference);
    ImportDuplicateDecision DecideImport(string incomingPath, string existingPath, bool existingReadOnly);
    void ShowReport(int selected, int quarantined, int skipped, int failed, long bytes);
}

public sealed class NullDuplicateDialogs : IDuplicateDialogs
{
    public DuplicateMergeScope? ChooseScope() => null;
    public HashSet<string>? Review(List<DuplicateCandidateGroup> candidates, string root, LibraryContentScanner scanner, bool showDifference) => null;
    public ImportDuplicateDecision DecideImport(string incomingPath, string existingPath, bool existingReadOnly) => ImportDuplicateDecision.Skip;
    public void ShowReport(int selected, int quarantined, int skipped, int failed, long bytes) { }
}
