using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

public sealed class PeopleAlbumViewModel : ObservableObject
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff"],
        StringComparer.OrdinalIgnoreCase);
    private readonly PeopleAlbumService _service;
    private readonly Func<IEnumerable<string>> _pathProvider;
    private PersonAlbumItemViewModel? _selectedAlbum;
    private bool _isScanning;
    private bool _areBubblesOpen;
    private string _statusText = "尚未扫描人物";

    public PeopleAlbumViewModel(PeopleAlbumService service, Func<IEnumerable<string>> pathProvider)
    {
        _service = service;
        _pathProvider = pathProvider;
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
        ToggleBubblesCommand = new RelayCommand(() => AreBubblesOpen = !AreBubblesOpen);
        ClearSelectionCommand = new RelayCommand(() => { SelectedAlbum = null; AreBubblesOpen = false; });
    }

    public ObservableCollection<PersonAlbumItemViewModel> Albums { get; } = [];
    public PersonAlbumItemViewModel? SelectedAlbum
    {
        get => _selectedAlbum;
        set => SetProperty(ref _selectedAlbum, value);
    }
    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value)) ScanCommand.NotifyCanExecuteChanged();
        }
    }
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }
    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IRelayCommand ToggleBubblesCommand { get; }
    public bool AreBubblesOpen { get => _areBubblesOpen; set => SetProperty(ref _areBubblesOpen, value); }

    public async Task InitializeAsync()
    {
        var snapshot = await _service.LoadAsync().ConfigureAwait(true);
        ReplaceAlbums(snapshot);
        StatusText = Albums.Count == 0 ? "点击扫描，在本机建立人物相册" : $"已保存 {Albums.Count} 个人物相册";
    }

    private Task ScanAsync() => ScanPathsAsync(_pathProvider());

    public async Task ScanPathsAsync(IEnumerable<string> sourcePaths)
    {
        if (IsScanning) return;
        IsScanning = true;
        StatusText = "正在本机检测人脸…";
        try
        {
            var paths = sourcePaths.Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var snapshot = await _service.ScanAsync(paths, default).ConfigureAwait(true);
            ReplaceAlbums(snapshot);
            StatusText = Albums.Count == 0 ? "没有检测到清晰人脸" : $"已建立 {Albums.Count} 个人物相册";
        }
        finally { IsScanning = false; }
    }

    private void ReplaceAlbums(PeopleAlbumSnapshot snapshot)
    {
        var selectedId = SelectedAlbum?.Id;
        Albums.Clear();
        foreach (var album in snapshot.Albums.OrderBy(album => album.Name, StringComparer.CurrentCultureIgnoreCase))
            Albums.Add(new PersonAlbumItemViewModel(album, _service, item =>
            {
                SelectedAlbum = item;
                AreBubblesOpen = false;
            }));
        SelectedAlbum = Albums.FirstOrDefault(album => album.Id == selectedId);
    }
}

public sealed class PersonAlbumItemViewModel : ObservableObject
{
    private readonly PeopleAlbumService _service;
    private string _name;

    public PersonAlbumItemViewModel(PersonAlbum album, PeopleAlbumService service, Action<PersonAlbumItemViewModel> select)
    {
        _service = service;
        Id = album.Id;
        _name = album.Name;
        CoverPath = album.CoverPath;
        PhotoPaths = album.PhotoPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        SelectCommand = new RelayCommand(() => select(this));
        SaveNameCommand = new AsyncRelayCommand(() => _service.RenameAsync(Id, Name, default));
    }

    public string Id { get; }
    public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
    public string CoverPath { get; }
    public HashSet<string> PhotoPaths { get; }
    public int PhotoCount => PhotoPaths.Count;
    public IRelayCommand SelectCommand { get; }
    public IAsyncRelayCommand SaveNameCommand { get; }
}
