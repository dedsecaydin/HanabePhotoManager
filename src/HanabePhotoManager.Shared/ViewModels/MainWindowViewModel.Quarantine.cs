using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Platform;

namespace HanabePhotoManager.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task OpenQuarantineAsync()
    {
        if (string.IsNullOrWhiteSpace(LibraryRoot)) { StatusMessage = "请先选择照片库。"; return; }
        if (PlatformServices.ShowQuarantine(LibraryRoot)) await RefreshLibraryAsync();
    }
}
