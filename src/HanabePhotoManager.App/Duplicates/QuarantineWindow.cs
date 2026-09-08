using System.Windows;
using System.Windows.Controls;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.App.Duplicates;

public sealed class QuarantineWindow : Window
{
    private readonly LibraryQuarantineService _service = new();
    private readonly string _root;
    private readonly System.Windows.Controls.ListBox _files = new() { SelectionMode = System.Windows.Controls.SelectionMode.Extended, DisplayMemberPath = nameof(QuarantinedFile.OriginalPath) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    public bool RestoredFiles { get; private set; }

    public QuarantineWindow(string libraryRoot)
    {
        _root = libraryRoot;
        Title = "安全隔离区";
        Width = 850; Height = 560; MinWidth = 600; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(StyleProperty, "Dialog.Window");
        var panel = new DockPanel();
        panel.SetResourceReference(MarginProperty, "Spacing.Page");
        var help = new TextBlock { Text = "隔离文件仍保存在照片库内，不会释放磁盘空间。选择文件可恢复到原路径；路径冲突时会保留隔离副本并说明原因。按 Ctrl 可多选。", TextWrapping = TextWrapping.Wrap };
        help.SetResourceReference(StyleProperty, "Dialog.Body");
        DockPanel.SetDock(help, Dock.Top); panel.Children.Add(help);
        var restore = new System.Windows.Controls.Button { Content = "恢复所选文件", HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        restore.SetResourceReference(StyleProperty, "Button.Primary");
        restore.Click += Restore_Click;
        DockPanel.SetDock(restore, Dock.Bottom); panel.Children.Add(restore);
        DockPanel.SetDock(_status, Dock.Bottom); panel.Children.Add(_status);
        _files.SetResourceReference(StyleProperty, "List.Default");
        panel.Children.Add(_files); Content = panel;
        Refresh();
    }

    private void Refresh()
    {
        try { _files.ItemsSource = _service.List(_root); _status.Text = $"可恢复文件：{_files.Items.Count} 个"; }
        catch (Exception ex) { _status.Text = "读取隔离记录失败，原文件未改动：" + ex.Message; }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var selected = _files.SelectedItems.Cast<QuarantinedFile>().ToArray();
        if (selected.Length == 0) { _status.Text = "请先选择要恢复的文件。"; return; }
        ((System.Windows.Controls.Button)sender).IsEnabled = false;
        var restored = 0;
        var errors = new List<string>();
        foreach (var entry in selected)
        {
            try { await Task.Run(() => _service.Restore(_root, entry)); restored++; RestoredFiles = true; }
            catch (Exception ex) { errors.Add($"{entry.OriginalPath}：{ex.Message}"); }
        }
        Refresh();
        _status.Text = $"已恢复 {restored} 个，失败 {errors.Count} 个。" + (errors.Count > 0 ? "\n" + string.Join("\n", errors) : string.Empty);
        ((System.Windows.Controls.Button)sender).IsEnabled = true;
    }
}
