using System.Windows;
using System.Windows.Media;
using System.IO;

namespace HanabePhotoManager.App.Services;

internal sealed class HanabeSoundService
{
    private readonly object _sync = new();
    private readonly List<MediaPlayer> _players = [];
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

    private void Play(string relativePath, double volume)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        _ = dispatcher.BeginInvoke(() =>
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) return;
                var player = new MediaPlayer { Volume = HanabeSoundPolicy.NormalizeVolume(volume) };
                void DisposePlayer(object? sender, EventArgs args)
                {
                    player.Close();
                    lock (_sync) _players.Remove(player);
                }
                player.MediaEnded += DisposePlayer;
                player.MediaFailed += (_, _) => DisposePlayer(null, EventArgs.Empty);
                lock (_sync) _players.Add(player);
                player.Open(new System.Uri(path));
                player.Play();
            }
            catch
            {
                // Sound feedback is optional and must never affect photo workflows.
            }
        });
    }
}
