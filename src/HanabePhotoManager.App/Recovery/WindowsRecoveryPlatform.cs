using HanabePhotoManager.Core.Recovery;
using System.IO;

namespace HanabePhotoManager.App.Recovery;

internal sealed class WindowsRecoveryPlatform : IRecoveryPlatform
{
    public IReadOnlyList<string> GetRemovableDevices() => DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable).Select(d => d.Name).ToArray();
    public string CreateOutputPath(string source, bool device) => RecoveryOutputDirectory.CreatePath(device ? "存储卡_" + source[0] : source, DateTime.Now);
    public void ValidateOutput(string output, long requiredBytes = 0) => RecoveryOutputDirectory.Validate(output, requiredBytes);
    public void ValidateDeviceOutput(string output, IRecoveryDeviceSession session)
    {
        if (!string.Equals(Path.GetPathRoot(Path.GetFullPath(output)), @"D:\", StringComparison.OrdinalIgnoreCase))
            throw new IOException("设备恢复只能写入已核对的 D 盘输出位置。");
        RecoveryOutputDirectory.Validate(output);
        session.ValidateDestination();
    }
    public void PrepareOutput(string output) => RecoveryOutputDirectory.Prepare(output);
    public async Task<IRecoveryDeviceSession> ConnectDeviceAsync(string device, CancellationToken token) => await RecoveryDeviceSession.ConnectAsync(device, token);
    public async Task<RecoveryPreviewResult> LoadPreviewAsync(string path, RecoveryCandidate candidate, CancellationToken token)
    {
        var result = await RecoveryPreviewService.LoadAsync(path, candidate, token);
        return new RecoveryPreviewResult(result.Image, result.Description);
    }
    public string DeviceSelectedMessage => "已选择存储卡。开始扫描时将请求 Windows 只读访问权限；输出目录已自动安排。";
}
