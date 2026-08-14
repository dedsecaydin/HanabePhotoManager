using System.Windows;
using System.IO;

namespace HanabePhotoManager.App.Duplicates;

public partial class ImportDuplicateBatchDecisionWindow : Window
{
    public ImportDuplicateBatchDecision Decision { get; private set; } = ImportDuplicateBatchDecision.SkipAll;

    public ImportDuplicateBatchDecisionWindow(IReadOnlyList<ImportDuplicateMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);
        InitializeComponent();
        DescriptionText.Text = $"本次导入中有 {matches.Count} 个文件与图库或本次选择的文件内容完全相同。请选择统一处理方式。";
        MatchesList.ItemsSource = matches
            .Take(100)
            .Select(match => $"{Path.GetFileName(match.IncomingPath)}  =  {Path.GetFileName(match.ExistingPath)}" +
                (match.ExistingIsReadOnlyRetouched ? "（修后：只读保留）" : string.Empty))
            .ToArray();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        Decision = ImportAllOption.IsChecked == true
            ? ImportDuplicateBatchDecision.ImportAll
            : DecideIndividuallyOption.IsChecked == true
                ? ImportDuplicateBatchDecision.DecideIndividually
                : ImportDuplicateBatchDecision.SkipAll;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
