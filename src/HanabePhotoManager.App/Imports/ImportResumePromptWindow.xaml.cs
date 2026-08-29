using System.Windows;

namespace HanabePhotoManager.App.Imports;

public enum ImportResumePromptAction { Continue, Discard, Later }

public partial class ImportResumePromptWindow : Window
{
    public ImportResumePromptAction Action { get; private set; } = ImportResumePromptAction.Later;

    public ImportResumePromptWindow(string summary)
    {
        InitializeComponent();
        SummaryText.Text = summary;
    }

    private void Continue_Click(object sender, RoutedEventArgs e) { Action = ImportResumePromptAction.Continue; DialogResult = true; }
    private void Discard_Click(object sender, RoutedEventArgs e) { Action = ImportResumePromptAction.Discard; DialogResult = true; }
    private void Later_Click(object sender, RoutedEventArgs e) { Action = ImportResumePromptAction.Later; DialogResult = false; }
}
