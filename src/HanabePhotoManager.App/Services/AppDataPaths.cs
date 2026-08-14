using System.IO;

namespace HanabePhotoManager.App.Services;

public static class AppDataPaths
{
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
