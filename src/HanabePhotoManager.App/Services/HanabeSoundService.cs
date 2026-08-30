using System.IO;
using System.Media;

namespace HanabePhotoManager.App.Services;

internal sealed class HanabeSoundService
{
    private readonly object _sync = new();
    private HanabeAssistantState _lastState = HanabeAssistantState.Idle;
    private DateTimeOffset _lastPlayedAt = DateTimeOffset.MinValue;

    internal void PlayState(HanabeAssistantState state, HanabeSoundSettings settings, bool force = false)
    {
        var asset = HanabeSoundPolicy.Resolve(state, settings);
        if (asset is null) return;
        lock (_sync)
        {
            if (!force && state == _lastState && DateTimeOffset.UtcNow - _lastPlayedAt < TimeSpan.FromSeconds(1)) return;
            _lastState = state;
            _lastPlayedAt = DateTimeOffset.UtcNow;
        }
        Play(asset, settings.Volume);
    }

    internal void Preview(HanabeSoundStyle style, double volume) =>
        PlayState(HanabeAssistantState.Completed, new(true, style, volume, false), force: true);

    private static void Play(string relativePath, double volume)
    {
        _ = Task.Run(() =>
        {
            try
            {
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
        });
    }
}
