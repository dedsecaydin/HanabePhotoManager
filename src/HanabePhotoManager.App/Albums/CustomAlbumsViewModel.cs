using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.Core.Albums;

namespace HanabePhotoManager.App.Albums;

public sealed partial class CustomAlbumsViewModel : ObservableObject
{
    private ICustomAlbumStore _store;
    private readonly CustomAlbumPhotoScanner _photoScanner;

    [ObservableProperty] private CustomAlbumItemViewModel? _selectedAlbum;
    [ObservableProperty] private string _editableDisplayName = string.Empty;
    [ObservableProperty] private string _statusMessage = "添加一个文件夹以开始浏览。";
    [ObservableProperty] private bool _isLoading;

    public CustomAlbumsViewModel(ICustomAlbumStore store, CustomAlbumPhotoScanner photoScanner)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _photoScanner = photoScanner ?? throw new ArgumentNullException(nameof(photoScanner));
        RenameSelectedCommand = new AsyncRelayCommand(RenameSelectedAsync, CanManageSelected);
        RemoveSelectedCommand = new AsyncRelayCommand(RemoveSelectedAsync, CanManageSelected);
        RefreshSelectedCommand = new AsyncRelayCommand(OpenSelectedAsync, CanManageSelected);
    }

    /// <summary>
    /// 替换相册存储（设置里更改了保存目录后调用），并重新加载现有相册。
    /// </summary>
    public async Task ReplaceStoreAsync(ICustomAlbumStore newStore, CancellationToken cancellationToken = default)
    {
        _store = newStore ?? throw new ArgumentNullException(nameof(newStore));
        Albums.Clear();
        Photos.Clear();
        SelectedAlbum = null;
        IsLoading = true;
        try
        {
            var albums = await _store.LoadAsync(cancellationToken).ConfigureAwait(true);
            foreach (var album in albums)
            {
                Albums.Add(new CustomAlbumItemViewModel(album));
            }

            StatusMessage = Albums.Count == 0
                ? "添加一个文件夹以开始浏览。"
                : $"已加载 {Albums.Count} 个自定义相册。";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public ObservableCollection<CustomAlbumItemViewModel> Albums { get; } = [];

    public ObservableCollection<CustomAlbumPhoto> Photos { get; } = [];

    public IAsyncRelayCommand RenameSelectedCommand { get; }

    public IAsyncRelayCommand RemoveSelectedCommand { get; }

    public IAsyncRelayCommand RefreshSelectedCommand { get; }

    public bool HasSelectedAlbum => SelectedAlbum is not null;

    public bool HasPhotos => Photos.Count > 0;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var albums = await _store.LoadAsync(cancellationToken).ConfigureAwait(true);
        Albums.Clear();
        foreach (var album in albums.OrderBy(album => album.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            Albums.Add(new CustomAlbumItemViewModel(album));
        }

        SelectedAlbum = Albums.FirstOrDefault();
        if (SelectedAlbum is null)
        {
            StatusMessage = "添加一个文件夹以开始浏览。";
        }
    }

    public async Task AddFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            StatusMessage = "请选择一个可访问的文件夹。";
            return;
        }

        var album = CustomAlbum.Create(Guid.NewGuid(), null, folderPath);
        if (Albums.Any(item => string.Equals(item.FolderPath, album.FolderPath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "该文件夹已经在自定义相册中。";
            return;
        }

        var item = new CustomAlbumItemViewModel(album);
        Albums.Add(item);
        await SaveAsync(cancellationToken).ConfigureAwait(true);
        SelectedAlbum = item;
        await OpenSelectedAsync().ConfigureAwait(true);
    }

    public async Task RenameSelectedAsync()
    {
        if (SelectedAlbum is null || string.IsNullOrWhiteSpace(EditableDisplayName))
        {
            StatusMessage = "请输入相册名称。";
            return;
        }

        SelectedAlbum.Rename(EditableDisplayName);
        await SaveAsync().ConfigureAwait(true);
        StatusMessage = "已更新相册显示名称；磁盘文件夹未改动。";
    }

    public async Task RemoveSelectedAsync()
    {
        if (SelectedAlbum is null)
        {
            return;
        }

        var removedName = SelectedAlbum.DisplayName;
        Albums.Remove(SelectedAlbum);
        Photos.Clear();
        SelectedAlbum = Albums.FirstOrDefault();
        await SaveAsync().ConfigureAwait(true);
        StatusMessage = $"已从应用中移除“{removedName}”；磁盘文件夹和照片未删除。";
    }

    public async Task OpenSelectedAsync()
    {
        if (SelectedAlbum is null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            SelectedAlbum.RefreshAvailability();
            var photos = await _photoScanner.ScanAsync(SelectedAlbum.FolderPath).ConfigureAwait(true);
            Photos.Clear();
            foreach (var photo in photos)
            {
                Photos.Add(photo);
            }

            StatusMessage = photos.Count == 0
                ? "该文件夹中没有可显示的图片。"
                : $"正在浏览“{SelectedAlbum.DisplayName}”中的 {photos.Count} 张图片。";
            OnPropertyChanged(nameof(HasPhotos));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "读取相册已取消。";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedAlbumChanged(CustomAlbumItemViewModel? value)
    {
        EditableDisplayName = value?.DisplayName ?? string.Empty;
        OnPropertyChanged(nameof(HasSelectedAlbum));
        RenameSelectedCommand.NotifyCanExecuteChanged();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        RefreshSelectedCommand.NotifyCanExecuteChanged();
        if (value is not null)
        {
            _ = OpenSelectedAsync();
        }
    }

    private bool CanManageSelected() => SelectedAlbum is not null && !IsLoading;

    private Task SaveAsync(CancellationToken cancellationToken = default) =>
        _store.SaveAsync(Albums.Select(item => item.Album).ToArray(), cancellationToken);
}
