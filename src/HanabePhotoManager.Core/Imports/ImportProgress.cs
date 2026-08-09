namespace HanabePhotoManager.Core.Imports;

/// <summary>
/// Immutable progress snapshot for an import batch. Units represent individual
/// source files, including files transferred together as one media group.
/// </summary>
public sealed record ImportProgress(int CompletedUnits, int TotalUnits, bool IsCanceled)
{
    public double Percentage => TotalUnits == 0 ? 100d : CompletedUnits * 100d / TotalUnits;

    public static ImportProgress Create(int totalUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalUnits);
        return new ImportProgress(0, totalUnits, false);
    }

    public ImportProgress Complete(int units)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        return this with { CompletedUnits = Math.Min(TotalUnits, CompletedUnits + units) };
    }

    public ImportProgress Cancel() => this with { IsCanceled = true };
}
