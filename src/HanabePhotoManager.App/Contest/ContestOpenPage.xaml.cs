using System.Windows.Controls;
using System.Windows.Input;

namespace HanabePhotoManager.App.Contest;

public partial class ContestOpenPage : System.Windows.Controls.UserControl
{
    private bool _browserInitialized;
    private static readonly ContestViewModel SharedVm = new();

    public ContestOpenPage()
    {
        InitializeComponent();
        OpenContestList.ItemsSource = SharedVm.OpenContests;
    }

    private async void ContestCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.FrameworkElement element
            || element.DataContext is not ContestItem contest)
            return;

        EmptyState.Visibility = System.Windows.Visibility.Collapsed;
        await InitializeBrowserAsync();
        Browser.CoreWebView2?.Navigate(contest.Url);
    }

    private async System.Threading.Tasks.Task InitializeBrowserAsync()
    {
        if (_browserInitialized) return;
        _browserInitialized = true;
        try
        {
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                userDataFolder: System.IO.Path.Combine(HanabePhotoManager.App.Services.AppDataPaths.Root, "WebView2", "ContestOpen"));
            await Browser.EnsureCoreWebView2Async(env);
        }
        catch
        {
            _browserInitialized = false;
        }
    }
}
