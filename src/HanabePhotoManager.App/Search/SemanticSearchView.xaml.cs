using System.Windows.Input;
using System.Windows;

namespace HanabePhotoManager.App.Search;

public partial class SemanticSearchView : System.Windows.Controls.UserControl
{
    public SemanticSearchView() => InitializeComponent();

    private void Result_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && ((FrameworkElement)sender).DataContext is SearchResultItemViewModel item) item.Open();
    }
}
