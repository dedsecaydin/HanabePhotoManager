using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App;

/// <summary>
/// 设置中心视图适配层，负责二级导航、主题预览和需要读取视觉树的交互同步。
/// </summary>
public partial class SettingsCenterPage : System.Windows.Controls.UserControl
{
    private static string? _lastSectionKey;

    public SettingsCenterPage()
    {
        InitializeComponent();
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
            // 回到设置页时停留在上次查看的分组，而不是每次都回到第一组。
            var remembered = SectionNavList.Items.OfType<ListBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, _lastSectionKey, StringComparison.Ordinal));
            if (remembered is not null) SectionNavList.SelectedItem = remembered;
            else SectionNavList.SelectedIndex = 0;
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

    private void SectionNav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SectionNavList.SelectedItem is not ListBoxItem { Tag: string key })
            return;
        _lastSectionKey = key;

        AppearanceSection.Visibility = key == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        SoundSection.Visibility = key == "sound" ? Visibility.Visible : Visibility.Collapsed;
        GeneralSection.Visibility = key == "general" ? Visibility.Visible : Visibility.Collapsed;
        LibrarySection.Visibility = key == "library" ? Visibility.Visible : Visibility.Collapsed;
        BrowseSection.Visibility = key == "browse" ? Visibility.Visible : Visibility.Collapsed;
        AdvancedSection.Visibility = key == "advanced" ? Visibility.Visible : Visibility.Collapsed;
        IntelligenceSection.Visibility = key == "intelligence" ? Visibility.Visible : Visibility.Collapsed;
        ToolsSection.Visibility = key == "tools" ? Visibility.Visible : Visibility.Collapsed;
        var section = key switch
        {
            "appearance" => AppearanceSection, "sound" => SoundSection,
            "general" => GeneralSection, "library" => LibrarySection,
            "browse" => BrowseSection, "intelligence" => IntelligenceSection,
            "tools" => ToolsSection, _ => AdvancedSection
        };
        AnchorList.ItemsSource = FindHeaders(section).Select(header => new SettingAnchor(header.Text, header)).ToArray();
        SettingsScroll.ScrollToTop();
        CurrentAnchorText.Text = "当前分组：顶部";
        SettingsContextHint.Text = key switch
        {
            "library" => "先确认照片库位置与导入分类，再调整扫描和去重选项。所有更改自动保存。",
            "browse" => "这里设置进入图库时的默认状态，不影响当前正在浏览的内容。",
            "appearance" => "主题立即生效；右侧预览会跟随选择变化。",
            "sound" => "先设置总开关与音量，再逐个试听事件提示。",
            "intelligence" => "AI 与人物识别的偏好集中在此；处理仍在本机进行。",
            "tools" => "常用图片工具的默认值集中在此。",
            "advanced" => "诊断、快捷键和关于信息放在最后，避免干扰日常设置。",
            _ => "应用启动与窗口行为集中在此。"
        };
        var showTheme = key == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        ThemePreviewHeading.Visibility = showTheme;
        ThemePreviewCard.Visibility = showTheme;
        var showStorage = key == "library" ? Visibility.Visible : Visibility.Collapsed;
        StorageHeading.Visibility = showStorage;
        StorageCard.Visibility = showStorage;
        var showAbout = key == "advanced" ? Visibility.Visible : Visibility.Collapsed;
        AboutHeading.Visibility = showAbout;
        AboutCard.Visibility = showAbout;
        SponsorDivider.Visibility = showAbout;
        SponsorHeading.Visibility = showAbout;
        SponsorQrCard.Visibility = showAbout;
        AfdianLinkCard.Visibility = showAbout;
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

    private sealed record SettingAnchor(string Text, FrameworkElement Target);
    private sealed record SettingSearchResult(string Text, string SectionName, string SectionKey, FrameworkElement Target);

    private void SettingsSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SectionNavList is null || SearchResultsScroll is null || LibrarySection is null) return;
        var query = SettingsSearchBox.Text.Trim();
        var searching = query.Length > 0;
        SectionNavList.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
        SearchResultsScroll.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        if (!searching) return;

        var sections = new (string Key, string Name, FrameworkElement Element)[]
        {
            ("library", "导入与照片库", LibrarySection), ("browse", "浏览与显示", BrowseSection),
            ("intelligence", "AI 与人物", IntelligenceSection), ("tools", "工具默认值", ToolsSection),
            ("appearance", "外观与导航", AppearanceSection), ("sound", "音效", SoundSection),
            ("general", "应用常规", GeneralSection), ("advanced", "高级与帮助", AdvancedSection)
        };
        var results = new List<SettingSearchResult>();
        foreach (var (key, name, section) in sections)
        {
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                results.Add(new SettingSearchResult(name, name, key, section));
            results.AddRange(FindSearchableLabels(section)
                .Where(label => label.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(label => new SettingSearchResult(label.Text, name, key, label)));
        }
        SearchResultsList.ItemsSource = results.Take(40).ToArray();
        SearchResultCount.Text = results.Count == 0 ? "没有找到匹配的设置" : $"找到 {results.Count} 项 · 点击直接定位";
        SearchResultsScroll.ScrollToTop();
    }

    private IEnumerable<TextBlock> FindSearchableLabels(DependencyObject parent)
    {
        var headingStyle = FindResource("Settings.GroupHeader");
        var titleStyle = FindResource("Settings.RowTitle");
        var descStyle = TryFindResource("Settings.RowDesc");
        return FindSearchableLabels(parent, headingStyle, titleStyle, descStyle);
    }

    // 搜索同时匹配分组标题、设置名与说明文字（说明命中时仍定位到该设置行）。
    private static IEnumerable<TextBlock> FindSearchableLabels(DependencyObject parent, object headingStyle, object titleStyle, object? descStyle)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is TextBlock text && !string.IsNullOrWhiteSpace(text.Text) &&
                (ReferenceEquals(text.Style, headingStyle) || ReferenceEquals(text.Style, titleStyle) ||
                 (descStyle is not null && ReferenceEquals(text.Style, descStyle))))
                yield return text;
            foreach (var label in FindSearchableLabels(child, headingStyle, titleStyle, descStyle)) yield return label;
        }
    }

    private void SettingsSearchResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: SettingSearchResult result }) return;
        var navigationItem = SectionNavList.Items.OfType<ListBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, result.SectionKey, StringComparison.Ordinal));
        if (navigationItem is null) return;
        SectionNavList.SelectedItem = navigationItem;
        SettingsSearchBox.Clear();
        Dispatcher.BeginInvoke(new Action(() => ScrollToSettingTarget(result.Target)), DispatcherPriority.Loaded);
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
            var item = SectionNavList.Items.OfType<ListBoxItem>()
                .FirstOrDefault(entry => string.Equals(entry.Tag as string, first.SectionKey, StringComparison.Ordinal));
            if (item is not null)
            {
                SectionNavList.SelectedItem = item;
                SettingsSearchBox.Clear();
                Dispatcher.BeginInvoke(new Action(() => ScrollToSettingTarget(first.Target)), DispatcherPriority.Loaded);
                e.Handled = true;
            }
        }
    }

    private void ScrollToSettingTarget(FrameworkElement target)
    {
        var offset = target.TranslatePoint(new System.Windows.Point(), SettingsScroll).Y + SettingsScroll.VerticalOffset;
        SettingsScroll.ScrollToVerticalOffset(Math.Max(0, offset - 12));
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
            if (current is System.Windows.Controls.Grid grid) { row = grid; break; }
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

    internal void ShowImportRoutingOverview() => ScrollToSettingTarget(ImportRoutingOverviewSection);

    private IEnumerable<TextBlock> FindHeaders(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is TextBlock text && ReferenceEquals(text.Style, FindResource("Settings.GroupHeader"))) yield return text;
            foreach (var header in FindHeaders(child)) yield return header;
        }
    }

    private void SettingAnchor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: FrameworkElement target })
        {
            ScrollToSettingTarget(target);
        }
    }

    private void SettingsScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (AnchorList.ItemsSource is not IEnumerable<SettingAnchor> anchors)
            return;

        var visible = anchors
            .Select(anchor => new
            {
                anchor.Text,
                Offset = anchor.Target.TranslatePoint(new System.Windows.Point(0, 0), SettingsScroll).Y
            })
            .Where(item => item.Offset <= 96)
            .OrderByDescending(item => item.Offset)
            .FirstOrDefault();

        CurrentAnchorText.Text = visible is null ? "当前分组：顶部" : $"当前分组：{visible.Text}";
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
