using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfCheckBox = System.Windows.Controls.CheckBox;

namespace HanabePhotoManager.App;

public partial class DuplicateReviewWindow : Window
{
    private readonly List<DuplicateItem> _items = [];

    public HashSet<string> FilesToDelete { get; } = new(StringComparer.OrdinalIgnoreCase);

    public DuplicateReviewWindow(List<List<string>> duplicateGroups)
    {
        InitializeComponent();
        SummaryText.Text = $"发现 {duplicateGroups.Count} 组重复内容，共 {duplicateGroups.Sum(g => g.Count)} 个文件。" +
                           "勾选要删除的文件（取消勾选=保留）。";
        BuildGroups(duplicateGroups);
    }

    private void BuildGroups(List<List<string>> groups)
    {
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var groupPanel = new StackPanel { Margin = new Thickness(0, i > 0 ? 12 : 0, 0, 0) };

            var header = new TextBlock
            {
                Text = $"第 {i + 1} 组 · {group.Count} 个重复文件",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            groupPanel.Children.Add(header);

            foreach (var path in group.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var checkbox = new WpfCheckBox
                {
                    Tag = path,
                    Margin = new Thickness(16, 2, 0, 2),
                    IsChecked = true
                };

                var fileName = Path.GetFileName(path);
                var dirName = Path.GetDirectoryName(path);
                var shortDir = string.IsNullOrEmpty(dirName) ? path : dirName;
                if (shortDir.Length > 60) shortDir = "..." + shortDir[^57..];
                var info = new TextBlock();
                info.Inlines.Add(fileName);
                info.Inlines.Add(new System.Windows.Documents.Run("  " + shortDir)
                {
                    FontSize = 11,
                    Foreground = System.Windows.Media.Brushes.Gray
                });
                checkbox.Content = info;

                if (_items.Count == 0 || _items.Last().GroupIndex != i)
                    checkbox.IsChecked = false;

                groupPanel.Children.Add(checkbox);
                _items.Add(new DuplicateItem(i, path, checkbox));
            }

            GroupsPanel.Children.Add(groupPanel);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        FilesToDelete.Clear();
        foreach (var item in _items)
        {
            if (item.Checkbox.IsChecked == true)
                FilesToDelete.Add(item.Path);
        }

        var keptByGroup = _items.GroupBy(i => i.GroupIndex)
            .ToDictionary(g => g.Key, g => g.Count(i => i.Checkbox.IsChecked != true));
        if (keptByGroup.Any(kvp => kvp.Value == 0))
        {
            System.Windows.MessageBox.Show(
                "每组重复内容至少需要保留一个文件，请取消至少一个文件的勾选。",
                "Hanabe", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed record DuplicateItem(int GroupIndex, string Path, WpfCheckBox Checkbox);
}
