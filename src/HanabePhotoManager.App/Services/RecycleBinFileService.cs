using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace HanabePhotoManager.App.Services;

public interface IRecycleBinFileService
{
    void MoveToRecycleBin(string path);
}

public sealed class RecycleBinFileService : IRecycleBinFileService
{
    public void MoveToRecycleBin(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("文件不存在。", path);
        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }
}
