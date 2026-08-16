using System.Windows;
using System.Windows.Controls;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;

namespace HanabePhotoManager.App;

public partial class SettingsCenterPage : System.Windows.Controls.UserControl
{
    public SettingsCenterPage() => InitializeComponent();

    private void SettingsCenterPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        TextNavigationRadio.IsChecked = viewModel.NavigationDisplayMode == NavigationDisplayMode.Text;
        IconNavigationRadio.IsChecked = viewModel.NavigationDisplayMode == NavigationDisplayMode.Icon;
        IconAndTextNavigationRadio.IsChecked = viewModel.NavigationDisplayMode == NavigationDisplayMode.IconAndText;

        if (SectionNavList.SelectedItem is null && SectionNavList.Items.Count > 0)
        {
            SectionNavList.SelectedIndex = 0;
        }

        UpdateThemeIndicators();
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

        AppearanceSection.Visibility = key == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        GeneralSection.Visibility = key == "general" ? Visibility.Visible : Visibility.Collapsed;
        LibrarySection.Visibility = key == "library" ? Visibility.Visible : Visibility.Collapsed;
        BrowseSection.Visibility = key == "browse" ? Visibility.Visible : Visibility.Collapsed;
        AdvancedSection.Visibility = key == "advanced" ? Visibility.Visible : Visibility.Collapsed;
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
