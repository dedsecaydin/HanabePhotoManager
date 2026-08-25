using System.Windows.Input;
using System.Windows;

namespace HanabePhotoManager.App.Search;

/// <summary>
/// 语义搜索结果视图，保留双击结果打开媒体的 WPF 输入适配。
/// </summary>
public partial class SemanticSearchView : System.Windows.Controls.UserControl
{
    public SemanticSearchView() => InitializeComponent();

    private void Result_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && ((FrameworkElement)sender).DataContext is SearchResultItemViewModel item) item.Open();
    }
}
