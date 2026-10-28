using System.Windows;
namespace HanabePhotoManager.App.Sessions;
public partial class SessionsWindow : Window
{
    public SessionsWindow(SessionsViewModel viewModel) { InitializeComponent(); DataContext = viewModel; Loaded += async (_, _) => await viewModel.InitializeAsync(); Closing += (_, _) => viewModel.CancelCommand.Execute(null); }
}
