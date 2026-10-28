using System.Windows;
using System.Windows.Input;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.ViewModels;
namespace HanabePhotoManager.App;
public partial class MainWindow
{
    private System.Windows.Point? _importDragStart;
    private void ImportThumbnail_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.CanEditImportOrganization || sender is not FrameworkElement { DataContext: ImportPreviewItemViewModel item }) return;
        _importDragStart = e.GetPosition(this);
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) item.IsOrganizeSelected = !item.IsOrganizeSelected;
        else if (!item.IsOrganizeSelected)
        {
            foreach (var row in _viewModel.ImportItems) row.IsOrganizeSelected = ReferenceEquals(row, item);
        }
    }
    private void ImportThumbnail_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_viewModel.CanEditImportOrganization || e.LeftButton != MouseButtonState.Pressed || _importDragStart is not { } start || sender is not FrameworkElement element) return;
        var point = e.GetPosition(this);
        if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _importDragStart = null;
        var rows = _viewModel.ImportItems.Where(i => i.IsOrganizeSelected).ToArray();
        if (rows.Length > 0) System.Windows.DragDrop.DoDragDrop(element, new System.Windows.DataObject(ImportOrganizationPanel.DragFormat, rows), System.Windows.DragDropEffects.Link);
    }
}
