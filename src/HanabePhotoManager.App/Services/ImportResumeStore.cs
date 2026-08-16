using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HanabePhotoManager.App.Services;

/// <summary>单个待导入文件组的续传快照（源路径 + 分类 + 目标日期）。</summary>
public sealed class ImportResumeEntry
{
    public string GroupKey { get; set; } = "";
    public string Category { get; set; } = "";
    public string PrimaryPath { get; set; } = "";
    public List<string> SidecarPaths { get; set; } = [];
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
}

/// <summary>导入续传状态：中断后重启据此提示继续，配合边传边验边删幂等重放。</summary>
public sealed class ImportResumeState
{
    public bool DeleteSourcesAfterVerify { get; set; }
    public List<ImportResumeEntry> Entries { get; set; } = [];
}

/// <summary>导入断点续传的持久化（JSON，位于应用数据目录）。</summary>
public sealed class ImportResumeStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    public ImportResumeStore(string? path = null)
    {
        _path = path ?? Path.Combine(AppDataPaths.Root, "import-resume.json");
    }

    public bool HasPending => File.Exists(_path);

    public ImportResumeState? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<ImportResumeState>(json, Options);
        }
        catch
        {
            return null;
        }
    }

    public void Save(ImportResumeState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(state, Options));
        }
        catch
        {
            // 持久化失败不应中断导入流程。
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch
        {
            // 清理失败可忽略。
        }
    }
}
