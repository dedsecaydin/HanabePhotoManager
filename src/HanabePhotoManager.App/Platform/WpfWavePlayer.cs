using System.IO;
using System.Media;

namespace HanabePhotoManager.App.Platform;

internal static class WpfWavePlayer
{
    public static void Play(byte[] wav)
    {
        using var stream = new MemoryStream(wav, writable: false);
        using var player = new SoundPlayer(stream);
        player.Load();
        player.PlaySync();
    }
}
