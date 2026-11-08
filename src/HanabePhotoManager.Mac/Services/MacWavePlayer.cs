using System.Diagnostics;

namespace HanabePhotoManager.Mac.Services;

internal static class MacWavePlayer
{
    public static void Play(byte[] wav)
    {
        var directory = Path.Combine(MacSettingsStore.DataDirectory, "sound-temp");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            File.WriteAllBytes(path, wav);
            var info = new ProcessStartInfo("/usr/bin/afplay") { UseShellExecute = false };
            info.ArgumentList.Add(path);
            using var process = Process.Start(info);
            process?.WaitForExit();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
