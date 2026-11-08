using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;
using System.IO;

namespace HanabePhotoManager.App.Services;

/// <summary>重新检查已冻结的目标路径，恢复时不重新编号或覆盖内容冲突。</summary>
public static class ImportResumePlanVerifier
{
    /// <summary>只凭持久化的校验凭据确认已删除来源，断开的来源绝不默认为完成。</summary>
    public static async Task<ImportPlanItem> ReconcileAsync(
        ImportResumeEntry entry, IFileHasher hasher, CancellationToken cancellationToken)
    {
        var plan = entry.Plan ?? throw new InvalidDataException("恢复记录缺少传输计划。");
        var remaining = new List<PlannedFile>();
        foreach (var file in plan.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(file.Source.FullPath))
            {
                remaining.Add(file);
                continue;
            }
            var receipt = entry.VerifiedFiles.FirstOrDefault(result =>
                string.Equals(result.File.Source.FullPath, file.Source.FullPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(result.File.DestinationPath, file.DestinationPath, StringComparison.OrdinalIgnoreCase));
            if (receipt is null || !File.Exists(file.DestinationPath) ||
                !string.Equals(await hasher.ComputeSha256Async(file.DestinationPath, cancellationToken).ConfigureAwait(false),
                    receipt.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"来源不可访问且无法确认目标完整，保留重试：{file.Source.FullPath}");
        }
        return await RefreshAsync(new ImportPlanItem(plan.Id, plan.Group, remaining, plan.Conflict, plan.State),
            hasher, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ImportPlanItem> RefreshAsync(
        ImportPlanItem plan, IFileHasher hasher, CancellationToken cancellationToken)
    {
        var probe = new DestinationProbe(hasher);
        var files = new List<PlannedFile>();
        foreach (var file in plan.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var conflict = await probe.CheckAsync(file.Source, file.DestinationPath, cancellationToken).ConfigureAwait(false);
            files.Add(file with { Conflict = conflict });
        }
        var aggregate = files.Any(file => file.Conflict == ConflictKind.SameNameDifferentContent)
            ? ConflictKind.SameNameDifferentContent
            : files.All(file => file.Conflict == ConflictKind.Identical) ? ConflictKind.Identical : ConflictKind.None;
        return new ImportPlanItem(plan.Id, plan.Group, files, aggregate, ImportItemState.Planned);
    }
}
