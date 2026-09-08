using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Duplicates;

namespace HanabePhotoManager.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task OpenQuarantineAsync()
    {
        if (string.IsNullOrWhiteSpace(LibraryRoot)) { StatusMessage = "请先选择照片库。"; return; }
        var window = new QuarantineWindow(LibraryRoot) { Owner = System.Windows.Application.Current?.MainWindow };
        window.ShowDialog();
        if (window.RestoredFiles) await RefreshLibraryAsync();
    }
}
