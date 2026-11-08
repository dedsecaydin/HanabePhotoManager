using LibVLCSharp.Shared;

namespace HanabePhotoManager.Mac.Services;

/// <summary>Owns the platform video player and releases native resources on close.</summary>
public sealed class NativeVideoSession : IDisposable
{
    private readonly LibVLC _library;
    private Media? _media;
    private bool _disposed;

    public NativeVideoSession()
    {
        if (OperatingSystem.IsMacOS())
        {
            var root = Path.Combine(AppContext.BaseDirectory, "vlc");
            var libraries = Path.Combine(root, "lib");
            var plugins = Path.Combine(root, "plugins");
            if (!Directory.Exists(libraries) || !Directory.Exists(plugins))
                throw new InvalidOperationException("视频播放组件缺失，请使用完整的 macOS 安装包。");
            Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", plugins);
            LibVLCSharp.Shared.Core.Initialize(libraries);
        }
        else LibVLCSharp.Shared.Core.Initialize();
        _library = new LibVLC("--no-video-title-show");
        Player = new MediaPlayer(_library);
    }

    public MediaPlayer Player { get; }
    public bool IsPlaying => Player.IsPlaying;
    public long TimeMilliseconds => Player.Time;

    public bool Play(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("找不到视频文件。", fullPath);
        Player.Stop();
        _media?.Dispose();
        _media = new Media(_library, fullPath, FromType.FromPath);
        return Player.Play(_media);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Player.Stop();
        _media?.Dispose();
        Player.Dispose();
        _library.Dispose();
    }
}
