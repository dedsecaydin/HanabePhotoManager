using System.Windows;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;

namespace HanabePhotoManager.App;

public partial class SettingsCenterPage : System.Windows.Controls.UserControl
{
    public SettingsCenterPage() => InitializeComponent();

    private void LightTheme_Click(object sender, RoutedEventArgs e) => ThemeManager.Apply(AppTheme.Light);

    private void DarkTheme_Click(object sender, RoutedEventArgs e) => ThemeManager.Apply(AppTheme.Dark);

    private void DynamicScheme_Click(object sender, RoutedEventArgs e) => ThemeManager.Apply(AppColorScheme.Dynamic);

    private void ForestScheme_Click(object sender, RoutedEventArgs e) => ThemeManager.Apply(AppColorScheme.Forest);

    private void VioletScheme_Click(object sender, RoutedEventArgs e) => ThemeManager.Apply(AppColorScheme.Violet);

    private void SettingsCenterPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        TextNavigationRadio.IsChecked = viewModel.NavigationDisplayMode == NavigationDisplayMode.Text;
        IconNavigationRadio.IsChecked = viewModel.NavigationDisplayMode == NavigationDisplayMode.Icon;
        IconAndTextNavigationRadio.IsChecked = viewModel.NavigationDisplayMode == NavigationDisplayMode.IconAndText;
    }

    private void NavigationDisplayMode_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is System.Windows.Controls.RadioButton { Tag: NavigationDisplayMode mode })
            viewModel.NavigationDisplayMode = mode;
    }
}
