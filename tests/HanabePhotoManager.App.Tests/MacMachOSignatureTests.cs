using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacMachOSignatureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerifierAcceptsIntactCodePagesAndRejectsTamperingAndMissingEntitlements(bool alternate)
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "macho-signature-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "fixture");
            var data = new byte[8192];
            void Little(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), value);
            void Big(int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset, 4), value);
            Little(0, 0xfeedfacf); Little(4, 0x0100000c); Little(16, 1); Little(20, 16);
            Little(32, 0x1d); Little(36, 16); Little(40, 4096); Little(44, alternate ? 180u : 96u);
            var hash = SHA256.HashData(data.AsSpan(0, 4096));
            Big(4096, 0xfade0cc0); Big(4100, alternate ? 180u : 96u); Big(4104, alternate ? 2u : 1u); Big(4108, 0); Big(4112, alternate ? 28u : 20u);
            var cd = 4116;
            if (alternate)
            {
                Big(4116, 4096); Big(4120, 104);
                Big(4124, 0xfade0c02); Big(4128, 76); data[4124 + 36] = 20; data[4124 + 37] = 1;
                cd = 4200;
            }
            Big(cd, 0xfade0c02); Big(cd + 4, 76); Big(cd + 8, 0x20500); Big(cd + 12, 0x10002);
            Big(cd + 16, 44); Big(cd + 24, 0); Big(cd + 28, 1); Big(cd + 32, 4096);
            data[cd + 36] = 32; data[cd + 37] = 2; data[cd + 39] = 12;
            hash.CopyTo(data, cd + 44);
            File.WriteAllBytes(path, data);
            Assert.Equal(0, Run(path, false));
            Assert.NotEqual(0, Run(path, true));
            data[100] = 1;
            File.WriteAllBytes(path, data);
            Assert.NotEqual(0, Run(path, false));
        }
        finally { Directory.Delete(root, true); }
    }

    private static int Run(string path, bool requireEntitlements)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "HanabePhotoManager.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var script = Path.Combine(root.FullName, "src", "HanabePhotoManager.Mac", "Packaging", "Test-MachOSignature.ps1");
        var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Path", path, "-ExpectedCpu", "16777228" }) info.ArgumentList.Add(arg);
        if (requireEntitlements) info.ArgumentList.Add("-RequireEntitlements");
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(); Task.WaitAll(output, error);
        return process.ExitCode;
    }
}
