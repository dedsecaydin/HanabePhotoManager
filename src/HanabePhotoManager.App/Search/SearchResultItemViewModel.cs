using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.Core.Search;

namespace HanabePhotoManager.App.Search;

public sealed class SearchResultItemViewModel : ObservableObject
{
    private ImageSource? _thumbnail;

    public SearchResultItemViewModel(SemanticSearchResult result)
    {
        FilePath = result.FileKey;
        Score = result.Score;
    }

    public string FilePath { get; }
    public string Name => Path.GetFileName(FilePath);
    public string Folder => Path.GetDirectoryName(FilePath) ?? string.Empty;
    public double Score { get; }
    public string ScoreText => $"相关度 {Score:P0}";
    public ImageSource? Thumbnail { get => _thumbnail; set => SetProperty(ref _thumbnail, value); }

    public void Open()
    {
        if (File.Exists(FilePath)) Process.Start(new ProcessStartInfo(FilePath) { UseShellExecute = true });
    }

    public void OpenFolder()
    {
        if (File.Exists(FilePath)) Process.Start("explorer.exe", $"/select,\"{FilePath}\"");
    }

    public async Task LoadThumbnailAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(FilePath)) return;
        try
        {
            var thumbnail = await Task.Run(() =>
            {
                var image = new BitmapImage();
                image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 320;
                image.UriSource = new Uri(FilePath, UriKind.Absolute); image.EndInit(); image.Freeze();
                return (ImageSource)image;
            }, cancellationToken).ConfigureAwait(false);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Thumbnail = thumbnail);
        }
        catch (OperationCanceledException) { }
        catch (NotSupportedException) { }
    }
}
