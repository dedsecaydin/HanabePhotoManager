using System.IO;
using Microsoft.Win32;

namespace HanabePhotoManager.App.WeChat;

public sealed class WeChatExecutableLocator
{
    private static readonly string[] ExecutableNames = ["Weixin.exe", "WeChat.exe"];

    public string? Locate(string? configuredPath = null)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var candidates = new List<string>();
        AddCandidate(candidates, configuredPath);
        foreach (var name in ExecutableNames)
        {
            AddCandidate(candidates, ReadAppPath(Registry.CurrentUser, name));
            AddCandidate(candidates, ReadAppPath(Registry.LocalMachine, name));
        }

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        }.Where(path => !string.IsNullOrWhiteSpace(path));

        foreach (var root in roots)
        {
            foreach (var relative in new[]
                     {
                         @"Tencent\WeChat\WeChat.exe",
                         @"Tencent\Weixin\Weixin.exe",
                         @"WeChat\WeChat.exe",
                         @"Weixin\Weixin.exe"
                     })
                AddCandidate(candidates, Path.Combine(root, relative));
        }

        var unique = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return unique.Length == 1 ? unique[0] : null;
    }

    public static bool IsCandidateName(string path) =>
        ExecutableNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

    private static void AddCandidate(ICollection<string> candidates, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !IsCandidateName(path) || !File.Exists(path))
            return;
        candidates.Add(Path.GetFullPath(path));
    }

    private static string? ReadAppPath(RegistryKey root, string executableName)
    {
        try
        {
            using var key = root.OpenSubKey(
                $@"Software\Microsoft\Windows\CurrentVersion\App Paths\{executableName}");
            return key?.GetValue(null) as string;
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
    }
}
