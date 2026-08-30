using System.Windows;

namespace HanabePhotoManager.App.Imports;

public partial class ImportMoveSafetyConfirmationWindow : Window
{
    public ImportMoveSafetyConfirmationWindow() => InitializeComponent();

    private void Window_Loaded(object sender, RoutedEventArgs e) => BackButton.Focus();
    private void Back_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void Confirm_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
}
