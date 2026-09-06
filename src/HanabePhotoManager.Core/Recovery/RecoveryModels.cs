namespace HanabePhotoManager.Core.Recovery;

public enum RecoveryCandidateStatus { Direct, Experimental, InsufficientEvidence, PossiblyOverwritten }
public enum RecoveryConfidence { Low, Medium, High }

public sealed record RecoveryCandidate(
    string Id, string ExpectedFileName, long StartOffset, long EndOffset,
    bool HasFtyp, bool HasMdat, bool HasMoov, bool HasSampleTables,
    bool IsFragmented, RecoveryConfidence Confidence, RecoveryCandidateStatus Status)
{
    public DateTime? LastWriteTime { get; init; }
    public string TimeDescription => LastWriteTime is { } time ? $"卡内最后写入：{time:yyyy-MM-dd HH:mm:ss}" : "写入时间未知";
    public string SizeDescription => $"{Length / 1048576d:N2} MB · 镜像偏移 {StartOffset:N0}";
    public string StatusDescription => CanRecoverDirectly ? "可导出副本" : "需要进一步分析";
    public long Length => EndOffset - StartOffset;
    public bool CanRecoverDirectly => Status == RecoveryCandidateStatus.Direct && HasFtyp && HasMdat && HasMoov && HasSampleTables && !IsFragmented;
}

public sealed record RecoveryScanResult(
    string ImagePath, long ImageLength, bool IsExFat, int SectorSize, int ClusterSize,
    IReadOnlyList<RecoveryCandidate> Candidates, DateTimeOffset ScannedAt);

public static class RecoverySafetyPolicy
{
    public static bool IsSupportedImage(string path) =>
        string.Equals(Path.GetExtension(path), ".img", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(path), ".raw", StringComparison.OrdinalIgnoreCase);

    public static bool CanExport(RecoveryCandidate candidate, long imageLength) =>
        candidate.CanRecoverDirectly && candidate.StartOffset >= 0 && candidate.EndOffset > candidate.StartOffset && candidate.EndOffset <= imageLength;
}
