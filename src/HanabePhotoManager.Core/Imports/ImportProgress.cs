namespace HanabePhotoManager.Core.Imports;

/// <summary>
/// 导入批次的不可变进度快照。计量单位是源文件，因此同一媒体组中的附属文件也会单独计数。
/// </summary>
public sealed record ImportProgress(int CompletedUnits, int TotalUnits, bool IsCanceled)
{
    /// <summary>当前完成比例；空任务直接视为 100%。</summary>
    public double Percentage => TotalUnits == 0 ? 100d : CompletedUnits * 100d / TotalUnits;

    /// <summary>创建尚未完成且未取消的初始进度。</summary>
    public static ImportProgress Create(int totalUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalUnits);
        return new ImportProgress(0, totalUnits, false);
    }

    /// <summary>增加完成单位，并将结果限制在总单位数以内。</summary>
    public ImportProgress Complete(int units)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        return this with { CompletedUnits = Math.Min(TotalUnits, CompletedUnits + units) };
    }

    /// <summary>返回标记为已取消的新快照。</summary>
    public ImportProgress Cancel() => this with { IsCanceled = true };
}
