namespace HanabePhotoManager.App.Services;

/// <summary>定义可恢复地移动文件到系统回收站的能力。</summary>
public interface IRecycleBinFileService
{
    void MoveToRecycleBin(string path);
}

/// <summary>Rejects deletion until a platform trash service is configured.</summary>
public sealed class UnavailableRecycleBinFileService : IRecycleBinFileService
{
    public void MoveToRecycleBin(string path) => throw new PlatformNotSupportedException("此平台尚未配置废纸篓服务。");
}
