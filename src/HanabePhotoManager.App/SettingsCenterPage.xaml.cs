using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;

namespace HanabePhotoManager.App;

public partial class SettingsCenterPage : System.Windows.Controls.UserControl
{
    public SettingsCenterPage() => InitializeComponent();

    private void LightTheme_Click(object sender, RoutedEventArgs e) => ThemeManager.Apply(AppTheme.Light);

    private void DarkTheme_Click(object sender, RoutedEventArgs e) => ThemeManager.Apply(AppTheme.Dark);

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

    private void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, SettingsTabs) || !IsLoaded)
            return;

        AnimateSettingsContent();
        if (SettingsTabs.SelectedItem is TabItem selected)
            AnimateSelectedTab(selected);
    }

    private void AnimateSettingsContent()
    {
        SettingsTabs.ApplyTemplate();
        if (SettingsTabs.Template.FindName("PART_SelectedContentHost", SettingsTabs) is not ContentPresenter host)
            return;

        if (!SystemParameters.ClientAreaAnimation)
        {
            host.Opacity = 1;
            host.RenderTransform = new TranslateTransform();
            return;
        }

        host.RenderTransform = new TranslateTransform(10, 0);
        var opacity = new DoubleAnimationUsingKeyFrames();
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        host.BeginAnimation(OpacityProperty, opacity, HandoffBehavior.SnapshotAndReplace);
        ((TranslateTransform)host.RenderTransform).BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private static void AnimateSelectedTab(TabItem selected)
    {
        if (!SystemParameters.ClientAreaAnimation)
            return;

        selected.RenderTransform = new TranslateTransform();
        selected.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(.55, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        ((TranslateTransform)selected.RenderTransform).BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(7, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }
}
