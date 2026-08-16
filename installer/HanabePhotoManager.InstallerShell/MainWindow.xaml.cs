using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace HanabePhotoManager.InstallerShell;

public partial class MainWindow : Window
{
    private readonly InstallerFlowState flow = new();
    private readonly InstallerEngine engine = new();
    private bool isDark;

    public MainWindow()
    {
        InitializeComponent();
        LicenseText.Text = ReadLicense();
        var systemColor = SystemParameters.WindowGlassColor;
        var brightness = (systemColor.R * 0.299 + systemColor.G * 0.587 + systemColor.B * 0.114) / 255;
        ApplyTheme(brightness < 0.45);
        RefreshView();
    }

    private static string ReadLicense()
    {
        var info = Application.GetResourceStream(new Uri("/Assets/license.txt", UriKind.Relative));
        if (info is null)
        {
            return "无法读取使用须知，请重新下载安装包。";
        }

        using var reader = new StreamReader(info.Stream);
        return reader.ReadToEnd();
    }

    private void ApplyTheme(bool dark)
    {
        isDark = dark;
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        });
        ThemeButton.Content = dark ? "浅色" : "深色";
    }

    private void RefreshView()
    {
        WelcomePanel.Visibility = flow.Step == InstallerStep.Welcome ? Visibility.Visible : Visibility.Collapsed;
        LicensePanel.Visibility = flow.Step == InstallerStep.License ? Visibility.Visible : Visibility.Collapsed;
        InstallingPanel.Visibility = flow.Step == InstallerStep.Installing ? Visibility.Visible : Visibility.Collapsed;
        CompletePanel.Visibility = flow.Step is InstallerStep.Complete or InstallerStep.Failed ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Visibility = flow.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
        NextButton.IsEnabled = flow.CanContinue || flow.Step is InstallerStep.Complete or InstallerStep.Failed;
        NextButton.Content = flow.Step switch
        {
            InstallerStep.Welcome => "下一步",
            InstallerStep.License => "开始安装",
            InstallerStep.Installing => "安装中…",
            InstallerStep.Complete => "启动应用",
            _ => "关闭"
        };

        StepOne.FontWeight = flow.Step == InstallerStep.Welcome ? FontWeights.Bold : FontWeights.Normal;
        StepTwo.FontWeight = flow.Step == InstallerStep.License ? FontWeights.Bold : FontWeights.Normal;
        StepThree.FontWeight = flow.Step == InstallerStep.Installing ? FontWeights.Bold : FontWeights.Normal;
        StepFour.FontWeight = flow.Step is InstallerStep.Complete or InstallerStep.Failed ? FontWeights.Bold : FontWeights.Normal;
        StepOne.Text = flow.Step == InstallerStep.Welcome ? "●  1  安装选项" : "✓  1  安装选项";
        StepTwo.Text = flow.Step switch { InstallerStep.Welcome => "○  2  使用须知", InstallerStep.License => "●  2  使用须知", _ => "✓  2  使用须知" };
        StepThree.Text = flow.Step == InstallerStep.Installing ? "●  3  正在安装" : flow.Step is InstallerStep.Complete or InstallerStep.Failed ? "✓  3  已完成" : "○  3  正在安装";
        StepFour.Text = flow.Step is InstallerStep.Complete or InstallerStep.Failed ? "●  4  完成" : "○  4  完成";
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (flow.Step == InstallerStep.Complete)
        {
            var executable = Path.Combine(InstallFolderBox.Text.Trim(), "HanabePhotoManager.App.exe");
            if (File.Exists(executable))
            {
                Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
            }
            Close();
            return;
        }

        if (flow.Step == InstallerStep.Failed)
        {
            Close();
            return;
        }

        flow.Continue();
        RefreshView();
        if (flow.Step != InstallerStep.Installing)
        {
            return;
        }

        try
        {
            var msiPath = engine.ExtractEmbeddedMsi();
            var outcome = await engine.InstallAsync(
                msiPath,
                InstallFolderBox.Text.Trim(),
                DesktopShortcutCheckBox.IsChecked == true,
                CancellationToken.None);
            var success = outcome is InstallerOutcome.Success or InstallerOutcome.RestartRequired;
            flow.Complete(success);
            ResultTitle.Text = success ? "安装完成" : outcome == InstallerOutcome.Cancelled ? "已取消安装" : "安装未完成";
            ResultMessage.Text = success
                ? outcome == InstallerOutcome.RestartRequired ? "安装成功，重新启动 Windows 后即可使用。" : "Hanabe Photo Manager 已准备就绪。"
                : $"Windows 安装服务未能完成操作。日志保存在：{engine.LogPath}";
            CopyLogButton.Visibility = success ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex)
        {
            flow.Complete(false);
            ResultTitle.Text = "安装未完成";
            ResultMessage.Text = ex.Message;
            CopyLogButton.Visibility = Visibility.Visible;
        }

        RefreshView();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        flow.Back();
        RefreshView();
    }

    private void LicenseScroll_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        if (!LicenseReadGate.HasReachedEnd(e.VerticalOffset, e.ViewportHeight, e.ExtentHeight))
        {
            return;
        }

        flow.MarkLicenseRead();
        AcceptCheckBox.IsEnabled = true;
        ReadHint.Text = "已阅读到末尾，可以确认同意";
        ReadHint.Foreground = (Brush)FindResource("Brush.Accent");
        RefreshView();
    }

    private void AcceptCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        flow.SetLicenseAccepted(AcceptCheckBox.IsChecked == true);
        RefreshView();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择安装位置", InitialDirectory = InstallFolderBox.Text };
        if (dialog.ShowDialog(this) == true)
        {
            InstallFolderBox.Text = Path.Combine(dialog.FolderName, "照片管理器");
        }
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e) => ApplyTheme(!isDark);

    private void CopyLogButton_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(engine.LogPath);

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (flow.Step == InstallerStep.Installing)
        {
            MessageBox.Show(this, "安装正在进行，请等待 Windows 安装服务完成。", "无法关闭", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
