namespace HanabePhotoManager.Core.Recovery;

public enum RecoveryCandidateStatus { Direct, Experimental, InsufficientEvidence, PossiblyOverwritten }
public enum RecoveryConfidence { Low, Medium, High }

public sealed record RecoveryCandidate(
    string Id, string ExpectedFileName, long StartOffset, long EndOffset,
    bool HasFtyp, bool HasMdat, bool HasMoov, bool HasSampleTables,
    bool IsFragmented, RecoveryConfidence Confidence, RecoveryCandidateStatus Status)
{
    public long Length => EndOffset - StartOffset;
    public bool CanRecoverDirectly => Status == RecoveryCandidateStatus.Direct && HasFtyp && HasMdat && HasMoov && !IsFragmented;
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
