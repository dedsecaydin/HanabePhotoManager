using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.App.Services;

/// <summary>单个待导入文件组的续传快照（源路径 + 分类 + 目标日期）。</summary>
/// <summary>可恢复导入队列中的单个源文件状态。</summary>
public sealed class ImportResumeEntry
{
    public string GroupKey { get; set; } = "";
    public string Category { get; set; } = "";
    public string PrimaryPath { get; set; } = "";
    public List<string> SidecarPaths { get; set; } = [];
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public string TargetDateDirectory { get; set; } = "";
    public ImportPlanItem? Plan { get; set; }
    public bool SkipTransfer { get; set; }
    public List<VerifiedFileResult> VerifiedFiles { get; set; } = [];
}

/// <summary>导入续传状态：中断后重启据此提示继续，配合边传边验边删幂等重放。</summary>
/// <summary>一次导入任务的版本化恢复快照。</summary>
public sealed class ImportResumeState
{
    public bool DeleteSourcesAfterVerify { get; set; }
    public string? NamingTemplate { get; set; }
    public List<ImportResumeEntry> Entries { get; set; } = [];
}

/// <summary>导入断点续传的持久化（JSON，位于应用数据目录）。</summary>
/// <summary>保存和读取导入恢复点，供异常退出后继续未完成任务。</summary>
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
            var state = JsonSerializer.Deserialize<ImportResumeState>(json, Options);
            return state?.Entries is null || state.Entries.Any(entry => entry is null || entry.SidecarPaths is null || entry.VerifiedFiles is null)
                ? null : state;
        }
        catch
        {
            return null;
        }
    }

    public void Save(ImportResumeState state)
    {
        var temporaryPath = _path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, state, Options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
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
