using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace HanabePhotoManager.App.Services;

/// <summary>使用 Windows Shell 文件操作把媒体移动到回收站。</summary>
public sealed class RecycleBinFileService : IRecycleBinFileService
{
    public void MoveToRecycleBin(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("文件不存在。", path);
        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }
}
