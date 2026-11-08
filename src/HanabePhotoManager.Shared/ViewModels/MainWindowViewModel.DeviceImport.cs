using System.IO;

namespace HanabePhotoManager.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool _promptOnMediaDevice = true;
    public bool PromptOnMediaDevice
    {
        get => _promptOnMediaDevice;
        set { if (SetProperty(ref _promptOnMediaDevice, value)) _ = SaveSettingsAsync(); }
    }

    /// <summary>用户确认后准备设备来源；不依赖设备概览缓存，也不在这里显示对话框。</summary>
    public async Task OfferDeviceImportAsync(string root)
    {
        if (!PromptOnMediaDevice || IsBusy) return;
        CurrentPage = "Import";
        if (!Directory.Exists(root))
        {
            StatusMessage = "设备已断开或无法读取，请重新连接后选择来源。";
            return;
        }
        var sourceRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var sourcePrefix = sourceRoot + Path.DirectorySeparatorChar;
        var target = HasLibraryRoot
            ? Path.GetFullPath(LibraryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : null;
        if (target is not null && (target.Equals(sourceRoot, StringComparison.OrdinalIgnoreCase) ||
                                  target.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "导入目标不能位于来源设备中，请先更换照片库位置。";
            return;
        }
        var device = ConnectedDevices.FirstOrDefault(d => string.Equals(d.Path, root, StringComparison.OrdinalIgnoreCase))
            ?? new ConnectedDeviceViewModel("媒体设备", "U盘 / 存储卡", root, true, "▣", "媒体设备", "已接入", root);
        await ImportFromDeviceAsync(device);
        if (!HasLibraryRoot)
            StatusMessage = "已进入导入并选中设备来源，请先选择目标照片库，再开始分析。";
    }
}
