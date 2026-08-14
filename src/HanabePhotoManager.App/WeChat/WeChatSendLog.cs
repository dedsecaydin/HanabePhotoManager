using System.IO;
using System.Text.Json;

namespace HanabePhotoManager.App.WeChat;

public sealed record WeChatSendLogEntry(
    DateTimeOffset Timestamp,
    Guid QueueItemId,
    string FileName,
    long Length,
    string TargetTitle,
    string TargetType,
    int Batch,
    int Attempt,
    WeChatSendItemState State,
    string Message);

public interface IWeChatSendLog
{
    Task AppendAsync(WeChatSendLogEntry entry, CancellationToken cancellationToken);
}

public sealed class JsonWeChatSendLog : IWeChatSendLog
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonWeChatSendLog(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HanabePhotoManager",
            "Logs",
            "wechat-send.jsonl");
    }

    public async Task AppendAsync(WeChatSendLogEntry entry, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
            await File.AppendAllTextAsync(_path, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
