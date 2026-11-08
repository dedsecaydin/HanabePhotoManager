using System.Text.Json;
using System.IO;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.Mac.Services;

public sealed class MacSettings
{
    public string LibraryRoot { get; set; } = string.Empty;
    public string Theme { get; set; } = "Dark";
    public bool MoveAfterVerify { get; set; }
    public bool SkipDuplicates { get; set; } = true;
    /// <summary>同一天有长时间间隔时自动分文件夹（与 Windows 共用 AppSettings.AutoSplitImportDays）。</summary>
    public bool AutoSplitImportDays { get; set; } = true;
    public int ImportDaySplitHours { get; set; } = 2;
}

/// <summary>
/// 设置保存位置：macOS 为 ~/Library/Application Support/HanabePhotoManager，
/// Windows（调试）为 %APPDATA%\HanabePhotoManager.Mac。可用 HANABE_MAC_DATA_DIR 覆盖（测试用）。
/// </summary>
public static class MacSettingsStore
{
    public static string SharedDataDirectory(string dataDirectory) => Path.Combine(dataDirectory, "shared");

    public static string DataDirectory
    {
        get
        {
            var overrideDir = Environment.GetEnvironmentVariable("HANABE_MAC_DATA_DIR");
            var dir = !string.IsNullOrWhiteSpace(overrideDir) ? overrideDir
                : OperatingSystem.IsMacOS()
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support", "HanabePhotoManager")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HanabePhotoManager.Mac");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public static MacSettings Load()
    {
        var store = new AppSettingsStore();
        MacSettingsMigration.MigrateAsync(store, SettingsPath).GetAwaiter().GetResult();
        var settings = store.LoadAsync().GetAwaiter().GetResult();
        return new MacSettings { LibraryRoot = settings.LibraryRoot ?? string.Empty, Theme = settings.MacTheme,
            MoveAfterVerify = settings.MacMoveAfterVerify, SkipDuplicates = settings.MacSkipDuplicates,
            AutoSplitImportDays = settings.AutoSplitImportDays, ImportDaySplitHours = settings.ImportDaySplitHours };
    }

    public static void Update(Action<AppSettings> change) => new AppSettingsStore().UpdateAsync(change).GetAwaiter().GetResult();
}
