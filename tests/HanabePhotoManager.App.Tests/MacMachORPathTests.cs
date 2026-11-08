using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacMachORPathTests
{
    [Fact]
    public void AddsRPathOnceAndRejectsInsufficientHeaderSpaceWithoutChangingFile()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "macho-rpath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var good = Path.Combine(root, "good");
            var bad = Path.Combine(root, "bad");
            File.WriteAllBytes(good, Fixture(4096));
            File.WriteAllBytes(bad, Fixture(190));
            Assert.Equal(0, Run(good));
            var updated = File.ReadAllBytes(good);
            Assert.Equal(2u, BitConverter.ToUInt32(updated, 16));
            Assert.Equal(0x8000001cu, BitConverter.ToUInt32(updated, 184));
            var firstHash = SHA256.HashData(updated);
            Assert.Equal(0, Run(good));
            Assert.Equal(firstHash, SHA256.HashData(File.ReadAllBytes(good)));
            var original = File.ReadAllBytes(bad);
            Assert.NotEqual(0, Run(bad));
            Assert.Equal(original, File.ReadAllBytes(bad));
        }
        finally { Directory.Delete(root, true); }
    }

    private static byte[] Fixture(uint firstSection)
    {
        var data = new byte[8192];
        void Write(int offset, uint value) => BitConverter.GetBytes(value).CopyTo(data, offset);
        Write(0, 0xfeedfacf); Write(4, 0x0100000c); Write(16, 1); Write(20, 152);
        Write(32, 0x19); Write(36, 152); Write(32 + 64, 1); Write(32 + 72 + 48, firstSection);
        return data;
    }

    private static int Run(string path)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "HanabePhotoManager.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var script = Path.Combine(root.FullName, "src", "HanabePhotoManager.Mac", "Packaging", "Add-MachORPath.ps1");
        var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Path", path, "-RPath", "@executable_path/vlc/lib" }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(); Task.WaitAll(output, error);
        return process.ExitCode;
    }
}
