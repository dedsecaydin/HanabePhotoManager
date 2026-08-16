using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace HanabePhotoManager.InstallerShell;

public sealed class InstallerEngine
{
    public string LogPath { get; } = Path.Combine(Path.GetTempPath(), "HanabePhotoManager", "install.log");

    public string ExtractEmbeddedMsi()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HanabePhotoManager", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "HanabePhotoManager-x64.msi");
        using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("EmbeddedInstaller.msi")
            ?? throw new InvalidOperationException("安装包中未找到内嵌 MSI。请重新下载安装包。");
        using var target = File.Create(destination);
        source.CopyTo(target);
        return destination;
    }

    public async Task<InstallerOutcome> InstallAsync(string msiPath, string installFolder, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        var arguments = $"/i \"{msiPath}\" INSTALLFOLDER=\"{installFolder}\" /qn /norestart /L*v \"{LogPath}\"";
        using var process = Process.Start(new ProcessStartInfo("msiexec.exe", arguments)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(msiPath)!
        }) ?? throw new InvalidOperationException("无法启动 Windows 安装服务。");

        await process.WaitForExitAsync(cancellationToken);
        return InstallerExitCode.Classify(process.ExitCode);
    }

    public async Task<int> RunQuietAsync(string msiPath, bool uninstall, string? requestedLogPath, CancellationToken cancellationToken)
    {
        var logPath = string.IsNullOrWhiteSpace(requestedLogPath) ? LogPath : Path.GetFullPath(requestedLogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var action = uninstall ? "/x" : "/i";
        using var process = Process.Start(new ProcessStartInfo("msiexec.exe", $"{action} \"{msiPath}\" /qn /norestart /L*v \"{logPath}\"")
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(msiPath)!
        }) ?? throw new InvalidOperationException("无法启动 Windows 安装服务。");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
