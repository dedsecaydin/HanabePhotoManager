namespace HanabePhotoManager.App.Imports;

/// <summary>导入向导顶部步骤条的一项。State 为 done / active / todo；IsClickable 时点击可跳到该步。</summary>
public sealed record ImportWizardStepItem(int Number, string Title, string Hint, string State, bool IsClickable = false)
{
    public string Marker => State == "done" ? "✓" : Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
