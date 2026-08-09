using WinForms = System.Windows.Forms;
using System.IO;

namespace HanabePhotoManager.App.Imports;

public interface IImportSourcePicker
{
    IReadOnlyList<string> PickFiles(string initialDirectory);
}

public sealed class WinFormsImportSourcePicker : IImportSourcePicker
{
    public IReadOnlyList<string> PickFiles(string initialDirectory)
    {
        using var dialog = new WinForms.OpenFileDialog
        {
            Title = "选择要导入的照片或视频",
            Filter = "媒体文件|*.jpg;*.jpeg;*.png;*.heic;*.dng;*.cr2;*.cr3;*.nef;*.arw;*.raf;*.rw2;*.orf;*.mp4;*.mov|所有文件 (*.*)|*.*",
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
            Multiselect = true,
            CheckFileExists = true,
            CheckPathExists = true
        };

        return dialog.ShowDialog() == WinForms.DialogResult.OK
            ? dialog.FileNames
            : Array.Empty<string>();
    }
}
