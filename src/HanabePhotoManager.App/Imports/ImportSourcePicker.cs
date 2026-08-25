using System.IO;

namespace HanabePhotoManager.App.Imports;

public interface IImportSourcePicker
{
    IReadOnlyList<string> PickFolders(string initialDirectory);
}

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
