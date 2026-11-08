namespace HanabePhotoManager.App.Platform;

public enum DialogSeverity { Information, Warning, Error }

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, bool dangerous = false);
    Task InfoAsync(string title, string message, DialogSeverity severity = DialogSeverity.Information);
    Task<string?> PickFolderAsync(string title);
    Task<IReadOnlyList<string>> PickFoldersAsync(string title);
    Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<(string Name, string[] Extensions)> filters, bool multiple);
    Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<(string Name, string[] Extensions)> filters);
}

public sealed class NullDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, bool dangerous = false) => Task.FromResult(false);
    public Task InfoAsync(string title, string message, DialogSeverity severity = DialogSeverity.Information) => Task.CompletedTask;
    public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
    public Task<IReadOnlyList<string>> PickFoldersAsync(string title) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<(string Name, string[] Extensions)> filters, bool multiple) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<(string Name, string[] Extensions)> filters) => Task.FromResult<string?>(null);
}
