using System.Diagnostics;
using HanabePhotoManager.App.Platform;

namespace HanabePhotoManager.Mac.Services;

internal sealed class MacSearchPlatform : ISearchPlatform
{
    public void Open(string path) => StartOpen(path, false);
    public void Reveal(string path) => StartOpen(path, true);
    public async Task<object?> LoadThumbnailAsync(string path, int width, CancellationToken token) =>
        await ThumbnailService.LoadAsync(path, width, token);
    private static void StartOpen(string path, bool reveal)
    {
        var info = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
        if (reveal) info.ArgumentList.Add("-R");
        info.ArgumentList.Add(path);
        Process.Start(info);
    }
}
