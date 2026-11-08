namespace HanabePhotoManager.App.DateFolders;

/// <summary>日期文件夹月历热力图中的一个月。</summary>
public sealed record DateHeatMonth(int Month, IReadOnlyList<DateHeatCell> Cells)
{
    public string Title => $"{Month} 月";
    public int FolderCount => Cells.Count(cell => cell.HasFolder);
}

/// <summary>月历中的一天；没有日期文件夹时为灰色且不可点击。</summary>
public sealed record DateHeatCell(int Month, int Day, DateFolderItemViewModel? Item)
{
    public bool HasFolder => Item is not null;
    public bool HasRemark => !string.IsNullOrWhiteSpace(Item?.OriginalRemark);
    public string ToolTipText => Item is null
        ? $"{Month} 月 {Day} 日 · 没有内容"
        : HasRemark ? $"{Month} 月 {Day} 日 · {Item.OriginalRemark}" : $"{Month} 月 {Day} 日 · 未写备注";
}
