using HanabePhotoManager.Core.Sessions;
using HanabePhotoManager.Infrastructure.Sessions;
namespace HanabePhotoManager.Infrastructure.Tests.Sessions;
public class JsonSessionStoreTests
{
    [Fact]
    public async Task UndoSurvivesRestartAndRejectsStaleRevision()
    {
        var dir = Path.Combine(@"D:\HanabePhoto\.artifacts\sessions-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { var path = Path.Combine(dir, "sessions.json"); var store = new JsonSessionStore(path); await store.CommitAsync(0, [new("a", "A", [])], new()); var reopened = new JsonSessionStore(path); Assert.NotNull((await reopened.LoadAsync()).Previous); await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.CommitAsync(0, [], new())); Assert.Empty((await reopened.UndoAsync(1)).Groups); }
        finally { Directory.Delete(dir, true); }
    }
    [Fact]
    public async Task UnknownVersionAndCorruptionPreserveBytes()
    {
        var dir = Path.Combine(@"D:\HanabePhoto\.artifacts\sessions-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { var path = Path.Combine(dir, "sessions.json"); var original = "{\"Version\":99,\"Revision\":0}"; await File.WriteAllTextAsync(path, original); await Assert.ThrowsAsync<InvalidDataException>(() => new JsonSessionStore(path).CommitAsync(0, [], new())); Assert.Equal(original, await File.ReadAllTextAsync(path)); }
        finally { Directory.Delete(dir, true); }
    }
    [Fact]
    public async Task BackupRestoresIntoCopyAndUndoRestoresPreferences()
    {
        var dir = Path.Combine(@"D:\HanabePhoto\.artifacts\sessions-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { var path = Path.Combine(dir, "sessions.json"); var store = new JsonSessionStore(path); await store.CommitAsync(0, [new("a", "A", [])], new(15, "文件名")); await store.CommitAsync(1, [new("b", "B", [])], new(60, "日期")); var bytes = await File.ReadAllBytesAsync(path); var copy = Path.Combine(dir, "restore", "sessions.json"); await store.VerifyBackupAsync(copy); Assert.Equal("a", (await new JsonSessionStore(copy).LoadAsync()).Groups[0].Id); Assert.Equal(bytes, await File.ReadAllBytesAsync(path)); var undone = await store.UndoAsync(2); Assert.Equal(15, undone.Preferences.ThresholdMinutes); Assert.Equal("文件名", undone.Preferences.Sort); }
        finally { Directory.Delete(dir, true); }
    }
    [Fact]
    public async Task ConcurrentInstancesOnlyPublishOneRevisionAndCancellationDoesNotPublish()
    {
        var dir = Path.Combine(@"D:\HanabePhoto\.artifacts\sessions-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "sessions.json"); await new JsonSessionStore(path).CommitAsync(0, [], new()); var bytes = await File.ReadAllBytesAsync(path); using (var cancellation = new CancellationTokenSource()) { cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new JsonSessionStore(path).CommitAsync(1, [], new(), cancellation.Token)); }
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            async Task<bool> Write(string id) { try { await new JsonSessionStore(path).CommitAsync(1, [new(id, id, [])], new()); return true; } catch (Exception e) when (e is IOException or InvalidOperationException) { return false; } }
            var result = await Task.WhenAll(Task.Run(() => Write("a")), Task.Run(() => Write("b"))); Assert.Single(result.Where(x => x)); Assert.Equal(2, (await new JsonSessionStore(path).LoadAsync()).Revision); Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        }
        finally { Directory.Delete(dir, true); }
    }
    [Fact]
    public async Task InvalidContentNeverOverwritesGoodState()
    {
        var dir = Path.Combine(@"D:\HanabePhoto\.artifacts\sessions-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { var path = Path.Combine(dir, "sessions.json"); var store = new JsonSessionStore(path); await store.CommitAsync(0, [], new()); var bytes = await File.ReadAllBytesAsync(path); var member = new CaptureMember("same", "same.jpg", null, null, "A"); await Assert.ThrowsAsync<InvalidDataException>(() => store.CommitAsync(1, [new("a", "A", [member]), new("b", "B", [member])], new())); Assert.Equal(bytes, await File.ReadAllBytesAsync(path)); await File.WriteAllTextAsync(path, "{broken}"); var corrupt = await File.ReadAllBytesAsync(path); await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => store.CommitAsync(1, [], new())); Assert.Equal(corrupt, await File.ReadAllBytesAsync(path)); }
        finally { Directory.Delete(dir, true); }
    }
}
