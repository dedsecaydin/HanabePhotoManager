using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.Core.Albums;
using System.IO;

namespace HanabePhotoManager.App.Albums;

public sealed partial class CustomAlbumItemViewModel : ObservableObject
{
    public CustomAlbumItemViewModel(CustomAlbum album)
    {
        Album = album;
    }

    public CustomAlbum Album { get; private set; }

    public string DisplayName => Album.DisplayName;

    public string FolderPath => Album.FolderPath;

    public bool IsFolderAvailable => Directory.Exists(FolderPath);

    public string AvailabilityText => IsFolderAvailable ? FolderPath : $"文件夹不可用：{FolderPath}";

    public void Rename(string displayName)
    {
        Album = CustomAlbum.Create(Album.Id, displayName, Album.FolderPath);
        OnPropertyChanged(nameof(DisplayName));
    }

    public void RefreshAvailability()
    {
        OnPropertyChanged(nameof(IsFolderAvailable));
        OnPropertyChanged(nameof(AvailabilityText));
    }
}
