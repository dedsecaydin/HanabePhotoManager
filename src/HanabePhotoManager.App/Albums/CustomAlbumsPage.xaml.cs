using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.Albums;

public partial class CustomAlbumsPage : System.Windows.Controls.UserControl
{
    private readonly PhotoDetailMetadataReader _metadataReader = new();
    private bool _showingAlbumDetail;
    private CustomAlbumPhoto? _selectedPhoto;

    public CustomAlbumsPage() => InitializeComponent();

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择要加入自定义相册的文件夹"
        };
        if (dialog.ShowDialog() == true && DataContext is CustomAlbumsViewModel viewModel)
        {
            await viewModel.AddFolderAsync(dialog.FolderName);
            _showingAlbumDetail = true;
            ApplyAlbumViewState();
        }
    }

    private void AlbumCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CustomAlbumItemViewModel item } &&
            DataContext is CustomAlbumsViewModel viewModel)
        {
            viewModel.SelectedAlbum = item;
            _showingAlbumDetail = true;
            ApplyAlbumViewState();
        }
    }

    private void BackToAlbums_Click(object sender, RoutedEventArgs e)
    {
        _showingAlbumDetail = false;
        ApplyAlbumViewState();
    }

    private void AlbumViewMode_Checked(object sender, RoutedEventArgs e)
    {
        // The Checked event fires while InitializeComponent is still building the
        // tree; the photo panels are not yet assigned at that point.
        if (AlbumPhotoGridPanel is null || AlbumPhotoListPanel is null)
        {
            return;
        }

        var isGrid = AlbumViewGridButton.IsChecked == true;
        AlbumPhotoGridPanel.Visibility = isGrid ? Visibility.Visible : Visibility.Collapsed;
        AlbumPhotoListPanel.Visibility = isGrid ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void AlbumPhoto_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CustomAlbumPhoto photo } ||
            DataContext is not CustomAlbumsViewModel viewModel)
        {
            return;
        }

        _selectedPhoto = photo;
        InspectorEmptyState.Visibility = Visibility.Collapsed;
        InspectorContent.Visibility = Visibility.Visible;
        InspectorPhotoName.Text = photo.Name;
        InspectorThumb.Source = LoadThumbnail(photo.FullPath, 360);
        InspectorAlbum.Text = viewModel.SelectedAlbum?.DisplayName ?? string.Empty;
        ApplyInspectorMetadata(PhotoDetailMetadata.Empty(photo.FullPath));

        try
        {
            var metadata = await System.Threading.Tasks.Task.Run(() => _metadataReader.Read(photo.FullPath));
            if (ReferenceEquals(_selectedPhoto, photo))
            {
                ApplyInspectorMetadata(metadata);
            }
        }
        catch
        {
            // A single unreadable photo must not crash the Inspector.
        }
    }

    private void Inspector_OpenOriginal(object sender, RoutedEventArgs e)
    {
        if (_selectedPhoto is { } photo && File.Exists(photo.FullPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(photo.FullPath) { UseShellExecute = true });
        }
    }

    private void Inspector_CopyPath(object sender, RoutedEventArgs e)
    {
        if (_selectedPhoto is { } photo)
        {
            try
            {
                System.Windows.Clipboard.SetText(photo.FullPath);
            }
            catch
            {
                // Clipboard can be locked by another process; ignore.
            }
        }
    }

    private void ApplyAlbumViewState()
    {
        AlbumOverviewPanel.Visibility = _showingAlbumDetail ? Visibility.Collapsed : Visibility.Visible;
        AlbumDetailPanel.Visibility = _showingAlbumDetail ? Visibility.Visible : Visibility.Collapsed;
        AlbumFab.Visibility = _showingAlbumDetail ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyInspectorMetadata(PhotoDetailMetadata metadata)
    {
        InspectorDimensions.Text = metadata.Dimensions;
        InspectorTakenAt.Text = metadata.TakenAt;
        InspectorCamera.Text = metadata.Camera;
        InspectorLens.Text = metadata.Lens;
        InspectorIso.Text = metadata.Iso;
        InspectorFileSize.Text = metadata.FileSize;
    }

    private static BitmapImage? LoadThumbnail(string path, int width)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = width;
            image.UriSource = new System.Uri(path, System.UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
