using HanabePhotoManager.App.Duplicates;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.App.Platform;

internal sealed class WpfDuplicateDialogs : IDuplicateDialogs
{
    public DuplicateMergeScope? ChooseScope()
    {
        var window = new DuplicateMergeScopeWindow { Owner = System.Windows.Application.Current?.MainWindow };
        return window.ShowDialog() == true ? window.Scope : null;
    }
    public HashSet<string>? Review(List<DuplicateCandidateGroup> candidates, string root, LibraryContentScanner scanner, bool showDifference)
    {
        var window = new DuplicateReviewWindow(candidates, root, scanner, showDifference) { Owner = System.Windows.Application.Current?.MainWindow };
        return window.ShowDialog() == true ? window.FilesToDelete : null;
    }
    public ImportDuplicateDecision DecideImport(string incomingPath, string existingPath, bool existingReadOnly)
    {
        var window = new ImportDuplicateDecisionWindow(incomingPath, existingPath, existingReadOnly) { Owner = System.Windows.Application.Current?.MainWindow };
        return window.ShowDialog() == true ? window.Decision : ImportDuplicateDecision.Skip;
    }
    public void ShowReport(int selected, int quarantined, int skipped, int failed, long bytes) =>
        new DuplicateCleanupReportWindow(selected, quarantined, skipped, failed, bytes) { Owner = System.Windows.Application.Current?.MainWindow }.ShowDialog();
}
