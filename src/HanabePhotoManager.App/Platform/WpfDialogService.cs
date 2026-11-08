using Microsoft.Win32;
using System.Windows;

namespace HanabePhotoManager.App.Platform;

internal sealed class WpfDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, bool dangerous = false)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        owner?.Activate();
        var icon = dangerous ? MessageBoxImage.Warning : MessageBoxImage.Question;
        var result = owner is null
            ? System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo, icon, MessageBoxResult.No)
            : System.Windows.MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, icon, MessageBoxResult.No);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }
    public Task InfoAsync(string title, string message, DialogSeverity severity = DialogSeverity.Information)
    {
        var icon = severity switch { DialogSeverity.Warning => MessageBoxImage.Warning, DialogSeverity.Error => MessageBoxImage.Error, _ => MessageBoxImage.Information };
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        return Task.CompletedTask;
    }
    public async Task<string?> PickFolderAsync(string title) => (await PickFoldersCoreAsync(title, false)).FirstOrDefault();
    public Task<IReadOnlyList<string>> PickFoldersAsync(string title) => PickFoldersCoreAsync(title, true);
    private static Task<IReadOnlyList<string>> PickFoldersCoreAsync(string title, bool multiple)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = multiple };
        return Task.FromResult<IReadOnlyList<string>>(dialog.ShowDialog() == true ? dialog.FolderNames : []);
    }
    public Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<(string Name, string[] Extensions)> filters, bool multiple)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = title, Multiselect = multiple, Filter = BuildFilter(filters) };
        return Task.FromResult<IReadOnlyList<string>>(dialog.ShowDialog() == true ? dialog.FileNames : []);
    }
    public Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<(string Name, string[] Extensions)> filters)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = title, FileName = suggestedName, Filter = BuildFilter(filters) };
        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }
    private static string BuildFilter(IReadOnlyList<(string Name, string[] Extensions)> filters) => filters.Count == 0
        ? "所有文件|*.*" : string.Join("|", filters.Select(f => $"{f.Name}|{string.Join(";", f.Extensions.Select(Pattern))}"));
    private static string Pattern(string extension) => extension.StartsWith('*') ? extension : "*." + extension.TrimStart('.');
}
