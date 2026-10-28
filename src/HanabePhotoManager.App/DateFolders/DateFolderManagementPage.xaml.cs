namespace HanabePhotoManager.App.DateFolders;

/// <summary>承载日期文件夹批量备注编辑的 WPF 页面。</summary>
public partial class DateFolderManagementPage
{
    public DateFolderManagementPage()
    {
        InitializeComponent();
    }

    private void DateFolderList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DateFolderList.SelectedItem is { } item) DateFolderList.ScrollIntoView(item);
    }
}
