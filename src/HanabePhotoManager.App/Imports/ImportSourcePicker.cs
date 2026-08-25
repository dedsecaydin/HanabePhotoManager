using System.IO;

namespace HanabePhotoManager.App.Imports;

/// <summary>定义一次选择一个或多个导入源文件夹的窗口边界。</summary>
public interface IImportSourcePicker
{
    IReadOnlyList<string> PickFolders(string initialDirectory);
}

/// <summary>使用 Windows 原生多选文件夹对话框选择相机来源。</summary>
public sealed class WindowsImportSourcePicker : IImportSourcePicker
{
    public IReadOnlyList<string> PickFolders(string initialDirectory)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择一个或多个相机来源文件夹",
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
            Multiselect = true
        };

        return dialog.ShowDialog() == true
            ? dialog.FolderNames
            : Array.Empty<string>();
    }
}
