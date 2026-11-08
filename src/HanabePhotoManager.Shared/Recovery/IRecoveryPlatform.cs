using HanabePhotoManager.Core.Recovery;

namespace HanabePhotoManager.App.Recovery;

internal interface IRecoveryDeviceSession : IDisposable
{
    Stream OpenRead();
    void ValidateDestination();
}

internal sealed record RecoveryPreviewResult(object? Image, string Description);

internal interface IRecoveryPlatform
{
    IReadOnlyList<string> GetRemovableDevices();
    string CreateOutputPath(string source, bool device);
    void ValidateOutput(string output, long requiredBytes = 0);
    void ValidateDeviceOutput(string output, IRecoveryDeviceSession session);
    void PrepareOutput(string output);
    Task<IRecoveryDeviceSession> ConnectDeviceAsync(string device, CancellationToken token);
    Task<RecoveryPreviewResult> LoadPreviewAsync(string path, RecoveryCandidate candidate, CancellationToken token);
    string DeviceSelectedMessage { get; }
}

internal sealed class UnavailableRecoveryPlatform : IRecoveryPlatform
{
    private static PlatformNotSupportedException Unavailable() => new("此平台尚未配置安全恢复服务。");
    public IReadOnlyList<string> GetRemovableDevices() => [];
    public string CreateOutputPath(string source, bool device) => throw Unavailable();
    public void ValidateOutput(string output, long requiredBytes = 0) => throw Unavailable();
    public void ValidateDeviceOutput(string output, IRecoveryDeviceSession session) => throw Unavailable();
    public void PrepareOutput(string output) => throw Unavailable();
    public Task<IRecoveryDeviceSession> ConnectDeviceAsync(string device, CancellationToken token) => Task.FromException<IRecoveryDeviceSession>(Unavailable());
    public Task<RecoveryPreviewResult> LoadPreviewAsync(string path, RecoveryCandidate candidate, CancellationToken token) => Task.FromResult(new RecoveryPreviewResult(null, "此平台尚未配置安全预览。"));
    public string DeviceSelectedMessage => "此平台尚未配置安全恢复服务。";
}
