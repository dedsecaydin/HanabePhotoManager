using System.Text.Json;
using HanabePhotoManager.Core.Sessions;
namespace HanabePhotoManager.Infrastructure.Sessions;
public sealed class JsonSessionStore(string path) : ISessionStore
{
    private readonly string _path = Path.GetFullPath(path);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public async Task<SessionDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return SessionDocument.Empty;
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var doc = await JsonSerializer.DeserializeAsync<SessionDocument>(stream, Options, cancellationToken) ?? throw new InvalidDataException("场次文件为空；原文件已保留。");
        if (doc.Version != 1) throw new InvalidDataException("不支持的场次版本；拒绝修改。");
        if (doc.Groups is null || doc.Preferences is null || doc.Revision < 0 || doc.Groups.Any(g => g is null || g.Members is null)) throw new InvalidDataException("场次文件结构损坏；原文件已保留。");
        Validate(doc); return doc;
    }
    public Task<SessionDocument> CommitAsync(long expectedRevision, IReadOnlyList<SessionGroup> groups, SessionPreferences preferences, CancellationToken cancellationToken = default) => Mutate(expectedRevision, d => d with { Revision = d.Revision + 1, Previous = d.Groups, PreviousPreferences = d.Preferences, Groups = groups, Preferences = preferences }, cancellationToken);
    public Task<SessionDocument> UndoAsync(long expectedRevision, CancellationToken cancellationToken = default) => Mutate(expectedRevision, d => d.Previous is null ? throw new InvalidOperationException("没有可撤销的提交。") : d with { Revision = d.Revision + 1, Groups = d.Previous, Preferences = d.PreviousPreferences ?? d.Preferences, Previous = null, PreviousPreferences = null }, cancellationToken);
    private static void Validate(SessionDocument doc)
    {
        static bool InvalidGroups(IReadOnlyList<SessionGroup> groups) => groups.Select(g => g?.Id).Distinct(StringComparer.Ordinal).Count() != groups.Count || groups.Any(g => g is null || string.IsNullOrWhiteSpace(g.Id) || g.Members is null || g.Members.Any(m => m is null || string.IsNullOrWhiteSpace(m.Id) || string.IsNullOrWhiteSpace(m.Path))) || groups.SelectMany(g => g.Members).GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1);
        static bool InvalidPreferences(SessionPreferences p) => p.Sort is not ("时间" or "日期" or "文件名") || !double.IsFinite(p.ThresholdMinutes) || p.ThresholdMinutes <= 0 || p.ThresholdMinutes > 525600 || p.CameraCorrections?.Values.Any(v => !double.IsFinite(v) || Math.Abs(v) > 525600) == true;
        if (InvalidGroups(doc.Groups) || (doc.Previous is not null && InvalidGroups(doc.Previous)) || InvalidPreferences(doc.Preferences) || (doc.PreviousPreferences is not null && InvalidPreferences(doc.PreviousPreferences))) throw new InvalidDataException("场次内容无效；原文件已保留。");
    }
    public async Task VerifyBackupAsync(string destination, CancellationToken token = default)
    {
        var backup = _path + ".backup";
        var document = await new JsonSessionStore(backup).LoadAsync(token);
        if (!File.Exists(backup)) throw new FileNotFoundException("尚无备份。", backup);
        var target = Path.GetFullPath(destination); if (target.Equals(_path, StringComparison.OrdinalIgnoreCase) || target.Equals(backup, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("恢复验证须使用副本路径。");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(backup, target, false);
        var restored = await new JsonSessionStore(target).LoadAsync(token);
        if (restored.Revision != document.Revision) throw new InvalidDataException("备份验证失败。");
    }
    private async Task<SessionDocument> Mutate(long revision, Func<SessionDocument, SessionDocument> update, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var gate = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var current = await LoadAsync(token); if (current.Revision != revision) throw new InvalidOperationException("场次已在其他窗口修改，请重新打开。 ");
        var next = update(current); Validate(next); var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { await JsonSerializer.SerializeAsync(stream, next, Options, token); await stream.FlushAsync(token); stream.Flush(true); } token.ThrowIfCancellationRequested(); if (File.Exists(_path)) File.Copy(_path, _path + ".backup", true); File.Move(temp, _path, true); return next; }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
