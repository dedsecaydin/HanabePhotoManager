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

    public DuplicateReviewWindow(List<DuplicateCandidateGroup> candidates)
    {
        InitializeComponent();
        var totalFiles = candidates.Sum(group => group.Paths.Count);
        var suspectedCount = candidates.Count(group => group.IsSuspected);
        SummaryText.Text = $"发现 {candidates.Count} 组重复内容，共 {totalFiles} 个文件。" +
                           (suspectedCount > 0
                               ? $"（其中 {suspectedCount} 组为视觉相似，建议人工确认后再删除）"
                               : string.Empty) +
                           "勾选要删除的文件（取消勾选=保留）。";
        BuildGroups(candidates);
    }

    private void BuildGroups(List<DuplicateCandidateGroup> candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            var group = candidates[i];
            var groupPanel = new StackPanel { Margin = new Thickness(0, i > 0 ? 12 : 0, 0, 0) };

            var headerText = $"第 {i + 1} 组 · {group.Paths.Count} 个重复文件";
            if (group.IsSuspected)
                headerText += " · 疑似（视觉相似）";

            var header = new TextBlock
            {
                Text = headerText,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4),
                Foreground = group.IsSuspected
                    ? System.Windows.Media.Brushes.OrangeRed
                    : System.Windows.Media.Brushes.Black
            };
            groupPanel.Children.Add(header);

            foreach (var path in group.Paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
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
