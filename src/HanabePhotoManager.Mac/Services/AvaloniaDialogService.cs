using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using HanabePhotoManager.App.Platform;

namespace HanabePhotoManager.Mac.Services;

internal sealed class AvaloniaDialogService : IDialogService
{
    private static Window? Owner => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public async Task<bool> ConfirmAsync(string title, string message, bool dangerous = false) => await ShowMessageAsync(title, message, true);
    public async Task InfoAsync(string title, string message, DialogSeverity severity = DialogSeverity.Information) => _ = await ShowMessageAsync(title, message, false);

    private static async Task<bool> ShowMessageAsync(string title, string message, bool confirm)
    {
        var owner = Owner;
        if (owner is null) return false;
        var window = new Window { Title = title, Width = 450, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        if (confirm)
        {
            var cancel = new Button { Content = "取消", IsDefault = true, IsCancel = true };
            cancel.Click += (_, _) => window.Close(false);
            buttons.Children.Add(cancel);
        }
        var ok = new Button { Content = "确定", IsDefault = !confirm };
        ok.Click += (_, _) => window.Close(true);
        buttons.Children.Add(ok);
        window.Content = new StackPanel { Margin = new Thickness(20), Spacing = 18, Children = { new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, buttons } };
        return await window.ShowDialog<bool>(owner);
    }

    public async Task<string?> PickFolderAsync(string title) => (await PickFoldersCoreAsync(title, false)).FirstOrDefault();
    public Task<IReadOnlyList<string>> PickFoldersAsync(string title) => PickFoldersCoreAsync(title, true);
    private static async Task<IReadOnlyList<string>> PickFoldersCoreAsync(string title, bool multiple)
    {
        if (Owner is not { } owner) return [];
        var results = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = multiple });
        return results.Select(item => item.TryGetLocalPath()).OfType<string>().ToArray();
    }
    public async Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<(string Name, string[] Extensions)> filters, bool multiple)
    {
        if (Owner is not { } owner) return [];
        var results = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = multiple, FileTypeFilter = Types(filters) });
        return results.Select(item => item.TryGetLocalPath()).OfType<string>().ToArray();
    }
    public async Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<(string Name, string[] Extensions)> filters)
    {
        if (Owner is not { } owner) return null;
        var result = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = title, SuggestedFileName = suggestedName, FileTypeChoices = Types(filters) });
        return result?.TryGetLocalPath();
    }
    private static IReadOnlyList<FilePickerFileType>? Types(IReadOnlyList<(string Name, string[] Extensions)> filters) =>
        filters.Count == 0 ? null : filters.Select(f => new FilePickerFileType(f.Name) { Patterns = f.Extensions.Select(e => e.StartsWith('*') ? e : "*." + e.TrimStart('.')).ToArray() }).ToArray();
}
