namespace HanabePhotoManager.App.Imports;

/// <summary>导入向导顶部步骤条的一项。State 为 done / active / todo。</summary>
public sealed record ImportWizardStepItem(int Number, string Title, string Hint, string State)
{
    public string Marker => State == "done" ? "✓" : Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
