using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App;

/// <summary>
/// 设置中心视图适配层：11 个分区的切换、全局搜索定位、主题色卡和需要读取视觉树的交互。
/// </summary>
public partial class SettingsCenterPage : System.Windows.Controls.UserControl
{
    private static string? _lastSectionKey;
    private readonly (string Key, string Name, FrameworkElement Element)[] _sections;

    /// <summary>搜索同义词：用户常用说法 → 设置里的叫法。</summary>
    private static readonly Dictionary<string, string[]> SearchSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["声音"] = ["音效", "提示音"], ["音量"] = ["提示音量"], ["缓存"] = ["指纹", "缓存"], ["开机"] = ["自启动"],
        ["壁纸"] = ["背景"], ["皮肤"] = ["主题", "服装"], ["换肤"] = ["主题"], ["人脸"] = ["人物", "人脸"],
        ["桌宠"] = ["Hanabe", "角色"], ["角色"] = ["服装", "角色"], ["重复"] = ["重复", "查重"], ["查重"] = ["重复", "指纹"],
        ["快捷"] = ["快捷键", "Ctrl"], ["版本"] = ["版本", "更新"], ["数据"] = ["数据目录", "缓存"], ["隐私"] = ["隐私", "本机"],
        ["命名"] = ["命名", "模板"], ["后缀"] = ["后缀", "扩展名"], ["格式"] = ["后缀", "格式"], ["深色"] = ["主题"], ["浅色"] = ["主题"]
    };

    public SettingsCenterPage()
    {
        InitializeComponent();
        _sections =
        [
            ("general", "常规", GeneralSection), ("appearance", "外观", AppearanceSection),
            ("pet", "Hanabe 桌宠", PetSection), ("import", "导入", ImportSection),
            ("library", "照片库", LibrarySection), ("browse", "浏览", BrowseSection),
            ("intelligence", "AI 与人物", IntelligenceSection), ("tools", "工具", ToolsSection),
            ("storage", "存储与隐私", StorageSection), ("shortcuts", "快捷键", ShortcutsSection),
            ("about", "关于", AboutSection)
        ];
        PreviewKeyDown += SettingsCenterPage_PreviewKeyDown;
    }

    private void SettingsCenterPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        TextNavigationRadio.IsChecked = viewModel.NavigationDisplayMode == NavigationDisplayMode.Text;
        IconNavigationRadio.IsChecked = viewModel.NavigationDisplayMode == NavigationDisplayMode.Icon;
        IconAndTextNavigationRadio.IsChecked = viewModel.NavigationDisplayMode == NavigationDisplayMode.IconAndText;

        if (SectionNavList.SelectedItem is null && SectionNavList.Items.Count > 0)
        {
            // 回到设置页时停留在上次查看的分区，而不是每次都回到第一组。
            if (!SelectSection(_lastSectionKey)) SectionNavList.SelectedIndex = 0;
        }

        UpdateThemeIndicators();
        if (App.OpenSettingsOverview)
            Dispatcher.BeginInvoke(new Action(ShowImportRoutingOverview), DispatcherPriority.Loaded);
    }

    private void NavigationDisplayMode_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is System.Windows.Controls.RadioButton { Tag: NavigationDisplayMode mode })
            viewModel.NavigationDisplayMode = mode;
    }

    private void ThemeCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag })
            return;

        var parts = tag.Split('.');
        var scheme = parts[0] switch
        {
            "Forest" => AppColorScheme.Forest,
            "Violet" => AppColorScheme.Violet,
            "Classic" => AppColorScheme.Classic,
            "Hanabe" => AppColorScheme.Hanabe,
            _ => AppColorScheme.Dynamic,
        };
        var theme = parts.Length > 1 && parts[1] == "Dark" ? AppTheme.Dark : AppTheme.Light;
        var window = Window.GetWindow(this);
        if (window is not null && sender is FrameworkElement source)
        {
            ThemeTransitionService.Apply(window, source, () => ThemeManager.Apply(theme, scheme));
        }
        else
        {
            ThemeManager.Apply(theme, scheme);
        }
        UpdateThemeIndicators();
    }

    private bool SelectSection(string? key)
    {
        var item = SectionNavList.Items.OfType<ListBoxItem>()
            .FirstOrDefault(entry => string.Equals(entry.Tag as string, key, StringComparison.Ordinal));
        if (item is null) return false;
        SectionNavList.SelectedItem = item;
        return true;
    }

    private void SectionNav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SectionNavList.SelectedItem is not ListBoxItem { Tag: string key })
            return;
        _lastSectionKey = key;
        foreach (var (sectionKey, _, element) in _sections)
            element.Visibility = sectionKey == key ? Visibility.Visible : Visibility.Collapsed;
        if (SettingsSearchBox.Text.Length > 0) SettingsSearchBox.Clear();
        SettingsScroll.ScrollToTop();
    }

    private void AddImportFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.AddImportCategoryFolder())
            NewImportFolderInput.Focus();
    }

    private void RemoveImportFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is System.Windows.Controls.Button { Tag: string folder })
            viewModel.RemoveImportCategoryFolder(folder);
    }

    private void NewImportFolderInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddImportFolder_Click(sender, e);
        e.Handled = true;
    }

    private void AddImportRule_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel) viewModel.AddImportRoutingRule();
    }

    private void PrepareImportRule_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not System.Windows.Controls.Button { Tag: string folder }) return;
        viewModel.NewRoutingTargetFolder = folder;
        NewRoutingPatternInput.Focus();
    }

    private void RemoveImportRule_Click(object sender, RoutedEventArgs e) => ChangeImportRule(sender, null);
    private void MoveImportRuleUp_Click(object sender, RoutedEventArgs e) => ChangeImportRule(sender, -1);
    private void MoveImportRuleDown_Click(object sender, RoutedEventArgs e) => ChangeImportRule(sender, 1);

    private void ChangeImportRule(object sender, int? move)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not System.Windows.Controls.Button { Tag: ImportRoutingRule rule }) return;
        var index = viewModel.ImportRoutingRules.ToList().FindIndex(item => ReferenceEquals(item, rule));
        if (move is { } delta) viewModel.MoveImportRoutingRule(index, delta);
        else viewModel.RemoveImportRoutingRule(index);
    }

    /// <summary>一条搜索结果：面包屑（分区 › 分组）、设置名，以及命中说明时的说明原文。</summary>
    private sealed record SettingSearchResult(string Text, string Breadcrumb, string Detail, string SectionKey, FrameworkElement Target)
    {
        public bool HasDetail => Detail.Length > 0;
    }

    private void SettingsSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchResultsScroll is null || SettingsScroll is null) return;
        var query = SettingsSearchBox.Text.Trim();
        var searching = query.Length > 0;
        SettingsScroll.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
        SearchResultsScroll.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        if (!searching) return;

        var terms = new List<string> { query };
        if (SearchSynonyms.TryGetValue(query, out var extra)) terms.AddRange(extra);
        bool Matches(string text) => terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

        var results = new List<SettingSearchResult>();
        var seen = new HashSet<FrameworkElement>();
        foreach (var (key, name, section) in _sections)
        {
            var group = "";
            TextBlock? lastTitle = null;
            foreach (var (label, kind) in FindSearchableLabels(section))
            {
                if (kind == LabelKind.Group) { group = label.Text; lastTitle = null; }
                else if (kind == LabelKind.Title) lastTitle = label;

                var crumb = group.Length > 0 ? $"{name}  ›  {group}" : name;
                if (kind == LabelKind.Group)
                {
                    if (Matches(label.Text) && seen.Add(label)) results.Add(new SettingSearchResult(label.Text, name, "", key, label));
                }
                else if (kind == LabelKind.Title)
                {
                    if (Matches(label.Text) && seen.Add(label)) results.Add(new SettingSearchResult(label.Text, crumb, "", key, label));
                }
                else if (Matches(label.Text) && lastTitle is not null && seen.Add(lastTitle))
                {
                    results.Add(new SettingSearchResult(lastTitle.Text, crumb, label.Text, key, lastTitle));
                }
            }
        }

        SearchResultsList.ItemsSource = results.Take(40).ToArray();
        SearchResultHeading.Text = $"“{query}”的结果";
        var sectionCount = results.Select(result => result.SectionKey).Distinct().Count();
        SearchResultCount.Text = results.Count == 0
            ? "没有找到匹配的设置。换个说法试试，比如下面这些。"
            : $"{results.Count} 项 · 在 {sectionCount} 个分区 · 点一项直接跳过去";
        SearchSuggestions.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SearchResultsScroll.ScrollToTop();
    }

    private enum LabelKind { Group, Title, Description }

    private IEnumerable<(TextBlock Label, LabelKind Kind)> FindSearchableLabels(DependencyObject parent)
    {
        var groupStyle = FindResource("Settings.GroupHeader");
        var titleStyle = FindResource("Settings.RowTitle");
        var descStyle = FindResource("Settings.RowDesc");
        return FindSearchableLabels(parent, groupStyle, titleStyle, descStyle);
    }

    // 按文档顺序遍历：分组标题、设置名与说明文字都可被搜索（说明命中时定位到所属设置行）。
    private static IEnumerable<(TextBlock, LabelKind)> FindSearchableLabels(DependencyObject parent, object groupStyle, object titleStyle, object descStyle)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is TextBlock text && !string.IsNullOrWhiteSpace(text.Text))
            {
                if (ReferenceEquals(text.Style, groupStyle)) yield return (text, LabelKind.Group);
                else if (ReferenceEquals(text.Style, titleStyle)) yield return (text, LabelKind.Title);
                else if (ReferenceEquals(text.Style, descStyle)) yield return (text, LabelKind.Description);
            }
            foreach (var label in FindSearchableLabels(child, groupStyle, titleStyle, descStyle)) yield return label;
        }
    }

    private void SettingsSearchResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: SettingSearchResult result }) OpenSearchResult(result);
    }

    private void OpenSearchResult(SettingSearchResult result)
    {
        if (!SelectSection(result.SectionKey)) return;
        SettingsSearchBox.Clear();
        Dispatcher.BeginInvoke(new Action(() => ScrollToSettingTarget(result.Target)), DispatcherPriority.Loaded);
    }

    private void SearchSuggestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Content: string text }) return;
        SettingsSearchBox.Text = text;
        SettingsSearchBox.CaretIndex = text.Length;
        SettingsSearchBox.Focus();
    }

    private void SettingsSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SettingsSearchBox.Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && SearchResultsList.ItemsSource is IEnumerable<SettingSearchResult> results &&
                 results.FirstOrDefault() is { } first)
        {
            OpenSearchResult(first);
            e.Handled = true;
        }
    }

    private void ScrollToSettingTarget(FrameworkElement target)
    {
        // 目标藏在「高级」折叠里时先展开，再计算位置。
        var expanded = false;
        for (DependencyObject? current = target; current is not null; current = LogicalTreeHelper.GetParent(current) ?? System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { Tag: ToggleButton { IsChecked: not true } toggle })
            {
                toggle.IsChecked = true;
                expanded = true;
            }
            if (ReferenceEquals(current, SettingsScroll)) break;
        }
        if (expanded) SettingsScroll.UpdateLayout();

        var offset = target.TranslatePoint(new System.Windows.Point(), SettingsScroll).Y + SettingsScroll.VerticalOffset;
        SettingsScroll.ScrollToVerticalOffset(Math.Max(0, offset - 24));
        FlashSettingRow(target);
    }

    /// <summary>定位后让目标设置行短暂高亮，便于一眼找到刚搜索/跳转到的那一项。</summary>
    private void FlashSettingRow(FrameworkElement target)
    {
        DependencyObject? current = target;
        System.Windows.Controls.Panel? row = null;
        for (var depth = 0; current is not null && depth < 4; depth++)
        {
            current = LogicalTreeHelper.GetParent(current) ?? System.Windows.Media.VisualTreeHelper.GetParent(current);
            if (current is System.Windows.Controls.DockPanel or System.Windows.Controls.Grid) { row = (System.Windows.Controls.Panel)current; break; }
        }
        if (row is null || row.ActualHeight > 160 || TryFindResource("Brush.Primary") is not System.Windows.Media.SolidColorBrush primary) return;

        var original = row.Background;
        var flash = new System.Windows.Media.SolidColorBrush(primary.Color) { Opacity = 0 };
        row.Background = flash;
        var animation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
        animation.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.22, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.22, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900))));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1600))));
        animation.Completed += (_, _) => { if (ReferenceEquals(row.Background, flash)) row.Background = original; };
        flash.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, animation);
    }

    /// <summary>设置页内按 Ctrl+F 直接聚焦设置搜索框。</summary>
    private void SettingsCenterPage_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SettingsSearchBox.Focus();
            SettingsSearchBox.SelectAll();
            e.Handled = true;
        }
    }

    internal void ShowImportRoutingOverview()
    {
        SelectSection("import");
        Dispatcher.BeginInvoke(new Action(() => ScrollToSettingTarget(ImportRoutingOverviewSection)), DispatcherPriority.Loaded);
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppDataPaths.Root);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{AppDataPaths.Root}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or System.IO.IOException or UnauthorizedAccessException)
        {
            // 打不开资源管理器时路径仍显示在卡片里，可手动复制。
        }
    }

    private void UpdateThemeIndicators()
    {
        var label = $"{SchemeName(ThemeManager.CurrentScheme)} · {(ThemeManager.Current == AppTheme.Light ? "浅色" : "深色")}";
        CurrentThemeTag.Text = label;

        var primary = (System.Windows.Media.Brush)TryFindResource("Brush.Primary")!;
        var outlineVariant = (System.Windows.Media.Brush)TryFindResource("Brush.OutlineVariant")!;
        ApplyThemeCard(ThemeCardDynamicLight, ThemeCheckDynamicLight, primary, outlineVariant);
        ApplyThemeCard(ThemeCardDynamicDark, ThemeCheckDynamicDark, primary, outlineVariant);
        ApplyThemeCard(ThemeCardForestLight, ThemeCheckForestLight, primary, outlineVariant);
        ApplyThemeCard(ThemeCardForestDark, ThemeCheckForestDark, primary, outlineVariant);
        ApplyThemeCard(ThemeCardVioletLight, ThemeCheckVioletLight, primary, outlineVariant);
        ApplyThemeCard(ThemeCardVioletDark, ThemeCheckVioletDark, primary, outlineVariant);
        ApplyThemeCard(ThemeCardClassicLight, ThemeCheckClassicLight, primary, outlineVariant);
        ApplyThemeCard(ThemeCardClassicDark, ThemeCheckClassicDark, primary, outlineVariant);
        ApplyThemeCard(ThemeCardHanabeLight, ThemeCheckHanabeLight, primary, outlineVariant);
        ApplyThemeCard(ThemeCardHanabeDark, ThemeCheckHanabeDark, primary, outlineVariant);
    }

    private void ApplyThemeCard(System.Windows.Controls.Button card, FrameworkElement check, System.Windows.Media.Brush primary, System.Windows.Media.Brush outlineVariant)
    {
        var current = $"{ThemeManager.CurrentScheme}.{ThemeManager.Current}";
        var active = card.Tag is string tag && string.Equals(tag, current, System.StringComparison.OrdinalIgnoreCase);
        card.BorderBrush = active ? primary : outlineVariant;
        card.BorderThickness = active ? new Thickness(2) : new Thickness(1);
        check.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string SchemeName(AppColorScheme scheme) => scheme switch
    {
        AppColorScheme.Forest => "森林绿",
        AppColorScheme.Violet => "紫罗兰",
        AppColorScheme.Classic => "经典",
        AppColorScheme.Hanabe => "樱粉镜面 · Hanabe",
        _ => "动态色彩",
    };

    private void SponsorQr_Click(object sender, RoutedEventArgs e)
    {
        // Extract the embedded sponsor QR to a temp file and open it with the
        // system's default image viewer (no external dependency, works after publish).
        var uri = new Uri("pack://application:,,,/Assets/wechat-sponsor-qr.jpg", UriKind.Absolute);
        using var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
        if (stream is null)
            return;

        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HanabePhotoManager-wechat-sponsor-qr.jpg");
        try
        {
            using (var target = System.IO.File.Create(tempPath))
                stream.CopyTo(target);
        }
        catch (System.IO.IOException)
        {
            return; // file already open elsewhere; the About card keeps showing the QR
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No default viewer registered — the QR stays visible in the About card.
        }
    }

    private void AfdianLink_Click(object sender, RoutedEventArgs e)
    {
        // Open the author's Afdian (爱发电) homepage in the default browser.
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://afdian.com/a/hanabededsec") { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No default browser registered.
        }
    }
}

/// <summary>把桌宠服装枚举映射到设置页服装卡片的预览图。</summary>
public sealed class AssistantOutfitPreviewConverter : IValueConverter
{
    private static readonly Dictionary<HanabeAssistantVisualStyle, BitmapImage?> Cache = [];

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not HanabeAssistantVisualStyle style) return null;
        if (Cache.TryGetValue(style, out var cached)) return cached;
        var file = style switch
        {
            HanabeAssistantVisualStyle.ChibiAnimated => "Outfits/street.png",
            HanabeAssistantVisualStyle.PixelAnimated => "Outfits/street.png",
            HanabeAssistantVisualStyle.IdolAnimated => "Outfits/idol.png",
            HanabeAssistantVisualStyle.RainyAnimated => "Outfits/rainy.png",
            HanabeAssistantVisualStyle.WinterAnimated => "Outfits/rainy.png",
            HanabeAssistantVisualStyle.OldMoneyAnimated => "Outfits/holy.png",
            HanabeAssistantVisualStyle.EncoreAnimated => "Outfits/encore.png",
            HanabeAssistantVisualStyle.Static => "hanabe-assistant.png",
            _ => null
        };
        BitmapImage? image = null;
        if (file is not null)
        {
            image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri($"pack://application:,,,/Assets/Hanabe/{file}", UriKind.Absolute);
            // 像素风：先缩到很小再用最近邻放大，卡片上就是像素颗粒。
            image.DecodePixelWidth = style == HanabeAssistantVisualStyle.PixelAnimated ? 64 : 200;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
        }
        Cache[style] = image;
        return image;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

/// <summary>把枚举值转成名称字符串，供只接受字符串参数的命令使用（例如声音风格试听）。</summary>
public sealed class EnumNameConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString();
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}
