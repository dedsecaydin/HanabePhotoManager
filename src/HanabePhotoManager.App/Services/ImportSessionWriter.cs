using System.IO;
using System.Security.Cryptography;
using System.Text;
using HanabePhotoManager.Core.Sessions;
using HanabePhotoManager.Infrastructure.Sessions;
namespace HanabePhotoManager.App.Services;

public static class ImportSessionWriter
{
    public static string StorePath(string libraryRoot)
    {
        var root = string.IsNullOrWhiteSpace(libraryRoot) ? "standalone" : Path.GetFullPath(libraryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        var scope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root)))[..16];
        return Path.Combine(AppDataPaths.ProcessingRoot, "sessions", scope + ".json");
    }
    public static async Task AssignAsync(ISessionStore store, ICaptureTimeReader reader, string name, string destination, CancellationToken token, string batchId = "legacy")
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var document = await store.LoadAsync(token);
        var member = reader.Read(destination);
        var groupId = "import-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(batchId + "\n" + name)))[..24];
        if (document.Groups.Any(g => g.Id == groupId && g.Members.Any(m => m.Id == member.Id))) return;
        var groups = document.Groups.Select(g => g with { Members = g.Members.Where(m => !string.Equals(m.Id, member.Id, StringComparison.OrdinalIgnoreCase)).ToArray() }).ToList();
        var index = groups.FindIndex(g => g.Id == groupId);
        if (index >= 0) groups[index] = groups[index] with { Members = groups[index].Members.Append(member).ToArray() };
        else groups.Add(new(groupId, name, [member], member.Issue is not null));
        await store.CommitAsync(document.Revision, groups.Where(g => g.Count > 0).ToArray(), document.Preferences, token);
    }
}
