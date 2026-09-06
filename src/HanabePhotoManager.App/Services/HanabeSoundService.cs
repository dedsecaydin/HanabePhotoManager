using System.IO;
using System.Media;

namespace HanabePhotoManager.App.Services;

internal sealed class HanabeSoundService
{
    private readonly object _sync = new();
    private HanabeSoundEvent? _lastEvent;
    private DateTimeOffset _lastPlayedAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _playback = new(1, 1);
    private long _generation;

    internal void PlayState(HanabeAssistantState state, HanabeSoundSettings settings, bool force = false)
    {
        var asset = HanabeSoundPolicy.Resolve(state, settings);
        if (asset is null) return;
        PlayResolved(asset, HanabeSoundPolicy.EventFor(state)!.Value, settings.Volume, force);
    }

    internal void PlayEvent(HanabeSoundEvent kind, HanabeSoundSettings settings, bool force = false)
    {
        var asset = HanabeSoundPolicy.Resolve(kind, settings);
        if (asset is not null) PlayResolved(asset, kind, settings.Volume, force);
    }

    private void PlayResolved(string asset, HanabeSoundEvent kind, double volume, bool force)
    {
        lock (_sync)
        {
            if (!force && kind == _lastEvent && DateTimeOffset.UtcNow - _lastPlayedAt < TimeSpan.FromSeconds(1)) return;
            _lastEvent = kind;
            _lastPlayedAt = DateTimeOffset.UtcNow;
        }
        Play(asset, volume);
    }

    internal void Preview(HanabeSoundStyle style, double volume) =>
        PlayState(HanabeAssistantState.Completed, new(true, style, volume, false), force: true);

    internal void PreviewEvent(HanabeSoundEvent kind, HanabeSoundStyle style, double volume) =>
        PlayEvent(kind, new(true, style, volume, false, OpenEnabled: true), force: true);

    private void Play(string relativePath, double volume)
    {
        var generation = Interlocked.Increment(ref _generation);
        _ = Task.Run(async () =>
        {
            await _playback.WaitAsync().ConfigureAwait(false);
            try
            {
                // Keep the newest pending cue; never accumulate a per-file sound queue.
                if (generation != Interlocked.Read(ref _generation)) return;
                var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) return;
                var wav = PcmWavVolumeScaler.Scale(File.ReadAllBytes(path), HanabeSoundPolicy.NormalizeVolume(volume));
                using var stream = new MemoryStream(wav, writable: false);
                using var player = new SoundPlayer(stream);
                player.Load();
                player.PlaySync();
            }
            catch
            {
                // Optional feedback must never interrupt photo workflows.
            }
            finally { _playback.Release(); }
        });
    }
}
