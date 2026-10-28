using System.Windows;
using HanabePhotoManager.App.ViewModels;
namespace HanabePhotoManager.App.Imports;
public partial class ImportOrganizationPanel : System.Windows.Controls.UserControl
{
    internal const string DragFormat = "Hanabe.ImportOrganization.Items";
    public ImportOrganizationPanel() => InitializeComponent();
    private void Target_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = DataContext is MainWindowViewModel { CanEditImportOrganization: true } && e.Data.GetDataPresent(DragFormat) ? System.Windows.DragDropEffects.Link : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }
    private void Category_Drop(object sender, System.Windows.DragEventArgs e) => Assign(sender, e, false);
    private void Session_Drop(object sender, System.Windows.DragEventArgs e) => Assign(sender, e, true);
    private void Assign(object sender, System.Windows.DragEventArgs e, bool session)
    {
        if (DataContext is MainWindowViewModel vm && sender is FrameworkElement target &&
            e.Data.GetData(DragFormat) is ImportPreviewItemViewModel[] items)
            vm.AssignImportOrganization(items, session ? null : target.DataContext as CategoryChoice, session ? target.DataContext as string : null);
        e.Handled = true;
    }
}
