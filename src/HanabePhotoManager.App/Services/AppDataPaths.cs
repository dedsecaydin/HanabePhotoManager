using System.IO;

namespace HanabePhotoManager.App.Services;

/// <summary>集中提供当前用户的设置、缓存、索引和托管资源目录。</summary>
public static class AppDataPaths
{
    /// <summary>新处理工作流的持久数据；验证进程沿用隔离根，默认保存在 D 盘项目内。</summary>
    public static string ProcessingRoot
    {
        get
        {
            var isolated = Environment.GetEnvironmentVariable("HANABE_APP_DATA_DIR");
            return ResolveProcessingRoot(isolated);
        }
    }

    internal static string ResolveProcessingRoot(string? isolated) => string.IsNullOrWhiteSpace(isolated)
        ? @"D:\HanabePhoto\data"
        : Path.Combine(Path.GetFullPath(isolated), "processing");

    public static string Root
    {
        get
        {
            var isolated = Environment.GetEnvironmentVariable("HANABE_APP_DATA_DIR");
            return string.IsNullOrWhiteSpace(isolated)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HanabePhotoManager")
                : Path.GetFullPath(isolated);
        }
    }

    /// <summary>
    /// 自定义相册持久化文件（custom-albums.json）的默认位置：始终位于应用数据目录内，
    /// 而不是用户选择的任意路径。用户仍可自行选择照片文件夹加入相册，但存储本身固定在此。
    /// </summary>
    public static string CustomAlbumsFile => Path.Combine(Root, "custom-albums.json");
}
