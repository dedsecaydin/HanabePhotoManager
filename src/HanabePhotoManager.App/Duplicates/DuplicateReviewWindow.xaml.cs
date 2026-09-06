using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HanabePhotoManager.Infrastructure.Files;
using WpfCheckBox = System.Windows.Controls.CheckBox;

namespace HanabePhotoManager.App;

/// <summary>
/// 重复内容复核窗口，让用户逐组选择需要删除的副本并保留最终决定。
/// </summary>
public partial class DuplicateReviewWindow : Window
{
    private readonly List<DuplicateItem> _items = [];
    private readonly string _libraryRoot;
    private readonly LibraryContentScanner _scanner;

    public HashSet<string> FilesToDelete { get; } = new(StringComparer.OrdinalIgnoreCase);

    public DuplicateReviewWindow(List<DuplicateCandidateGroup> candidates, string libraryRoot, LibraryContentScanner? scanner = null, bool showDifferenceGrid = true)
    {
        _libraryRoot = libraryRoot;
        _scanner = scanner ?? new LibraryContentScanner(new Sha256FileHasher());
        InitializeComponent();
        ShowDifferenceGrid.IsChecked = showDifferenceGrid;
        var totalFiles = candidates.Sum(group => group.Paths.Count);
        var suspectedCount = candidates.Count(group => group.IsSuspected);
        SummaryText.Text = $"发现 {candidates.Count} 组重复内容，共 {totalFiles} 个文件。" +
                           (suspectedCount > 0
                               ? $"（其中 {suspectedCount} 组为视觉相似，建议人工确认后再删除）"
                               : string.Empty) +
                           "勾选要删除的文件（取消勾选=保留）。";
        BuildGroups(candidates);
        Closed += (_, _) => _comparisonCancellation?.Cancel();
        if (candidates.FirstOrDefault() is { } first && first.Paths.Count > 1)
            _ = ShowComparisonAsync(first, first.Paths[1]);
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
                TextWrapping = TextWrapping.Wrap
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Text.Primary");
            groupPanel.Children.Add(header);

            foreach (var path in group.Paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var checkbox = new WpfCheckBox
                {
                    Tag = path,
                    Margin = new Thickness(16, 2, 0, 2),
                    IsChecked = !group.IsSuspected
                };
                checkbox.SetResourceReference(FrameworkElement.StyleProperty, "Selection.CheckBox");

                var fileName = Path.GetFileName(path);
                var dirName = Path.GetDirectoryName(path);
                var shortDir = string.IsNullOrEmpty(dirName) ? path : dirName;
                if (shortDir.Length > 60) shortDir = "..." + shortDir[^57..];
                var info = new TextBlock { TextWrapping = TextWrapping.Wrap, ToolTip = path };
                info.Inlines.Add(fileName);
                info.Inlines.Add(new System.Windows.Documents.Run("  " + shortDir)
                {
                    FontSize = 11,
                    Foreground = (System.Windows.Media.Brush)FindResource("Brush.Text.Secondary")
                });
                checkbox.Content = info;

                if (_items.Count == 0 || _items.Last().GroupIndex != i)
                    checkbox.IsChecked = false;

                if (RetouchedDirectoryPolicy.IsReadOnlyRetouchedPath(_libraryRoot, path))
                {
                    checkbox.IsChecked = false;
                    checkbox.IsEnabled = false;
                    info.Inlines.Add(new System.Windows.Documents.Run("  只读保留")
                    {
                        FontSize = 11,
                        Foreground = (System.Windows.Media.Brush)FindResource("Brush.Primary")
                    });
                }

                groupPanel.Children.Add(checkbox);
                var compare = new System.Windows.Controls.Button { Content = "查看对照" };
                compare.SetResourceReference(StyleProperty, "Button.Secondary");
                compare.Click += async (_, _) => await ShowComparisonAsync(group, path);
                groupPanel.Children.Add(compare);
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
            if (item.Checkbox.IsChecked == true &&
                !RetouchedDirectoryPolicy.IsReadOnlyRetouchedPath(_libraryRoot, item.Path))
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
