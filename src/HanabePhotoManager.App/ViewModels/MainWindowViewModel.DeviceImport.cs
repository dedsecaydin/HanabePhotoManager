using System.IO;
using System.Windows;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool _promptOnMediaDevice = true;
    public bool PromptOnMediaDevice
    {
        get => _promptOnMediaDevice;
        set { if (SetProperty(ref _promptOnMediaDevice, value)) _ = SaveSettingsAsync(); }
    }

    public async Task OfferDeviceImportAsync(string root)
    {
        if (!PromptOnMediaDevice || IsBusy || !Directory.Exists(root)) return;
        var destination = HasLibraryRoot ? LibraryRoot : "尚未指定（下一步选择）";
        var answer = System.Windows.MessageBox.Show(System.Windows.Application.Current.MainWindow,
            $"检测到 {root} 中有可导入的照片或视频。\n\n目标照片库：{destination}\n\n是否进入导入页面？你可以检查目标位置和文件后开始导入。",
            "检测到媒体设备", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes || !Directory.Exists(root)) return;
        if (!HasLibraryRoot)
        {
            var picker = new Microsoft.Win32.OpenFolderDialog { Title = "选择导入目标照片库" };
            if (picker.ShowDialog() != true) return;
            LibraryRoot = picker.FolderName;
        }
        var target = Path.GetFullPath(LibraryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (target.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "导入目标不能位于来源设备中，请先更换照片库位置。";
            return;
        }
        RefreshConnectedDevices();
        var device = ConnectedDevices.FirstOrDefault(d => string.Equals(d.Path, root, StringComparison.OrdinalIgnoreCase));
        if (device is not null) await ImportFromDeviceAsync(device);
    }
}
