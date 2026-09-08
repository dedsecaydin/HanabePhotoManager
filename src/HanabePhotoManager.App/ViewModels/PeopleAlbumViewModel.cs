using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.People;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.Core.Performance;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>协调人物扫描、人物相册选择和相册内照片展示。</summary>
public sealed class PeopleAlbumViewModel : ObservableObject
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff"],
        StringComparer.OrdinalIgnoreCase);
    private readonly PeopleAlbumService _service;
    private readonly Func<IEnumerable<string>> _pathProvider;
    private readonly Func<IReadOnlyList<PersonAlbumItemViewModel>, PersonAlbumItemViewModel?> _mergeTargetPicker;
    private PersonAlbumItemViewModel? _selectedAlbum;
    private bool _isScanning;
    private bool _areBubblesOpen;
    private string _statusText = "尚未扫描人物";
    private CancellationTokenSource? _scanCancellation;
    private double _scanProgressValue;
    private int _detectedFaceCount;
    private PersonPhotoViewModel? _selectedPhoto;
    private bool _isEditing;
    private bool _canUndo;
    public PersonPhotoViewModel? SelectedPhoto { get => _selectedPhoto; set { if (SetProperty(ref _selectedPhoto, value)) NotifyEditingCommands(); } }
    public bool IsEditing { get => _isEditing; private set { if (SetProperty(ref _isEditing, value)) NotifyEditingCommands(); } }
    public bool CanUndo { get => _canUndo; private set { if (SetProperty(ref _canUndo, value)) UndoCommand.NotifyCanExecuteChanged(); } }
    public IAsyncRelayCommand RemovePhotoCommand { get; }
    public IAsyncRelayCommand SplitPhotoCommand { get; }
    public IAsyncRelayCommand UndoCommand { get; }

    public PeopleAlbumViewModel(
        PeopleAlbumService service,
        Func<IEnumerable<string>> pathProvider,
        Func<IReadOnlyList<PersonAlbumItemViewModel>, PersonAlbumItemViewModel?>? mergeTargetPicker = null)
    {
        _service = service;
        _pathProvider = pathProvider;
        _mergeTargetPicker = mergeTargetPicker ?? ShowMergeDialog;
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning && !IsEditing);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        ToggleBubblesCommand = new RelayCommand(() => AreBubblesOpen = !AreBubblesOpen);
        ClearSelectionCommand = new RelayCommand(() => { SelectedAlbum = null; AreBubblesOpen = false; });
        MergeCommand = new AsyncRelayCommand(MergeSelectedAsync, CanMerge);
        RemovePhotoCommand = new AsyncRelayCommand(() => EditPhotoAsync(false), CanEditPhoto);
        SplitPhotoCommand = new AsyncRelayCommand(() => EditPhotoAsync(true), CanEditPhoto);
        UndoCommand = new AsyncRelayCommand(UndoAsync, () => CanUndo && !IsScanning && !IsEditing);
        Albums.CollectionChanged += (_, _) => MergeCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<PersonAlbumItemViewModel> Albums { get; } = [];
    public PersonAlbumItemViewModel? SelectedAlbum
    {
        get => _selectedAlbum;
        set
        {
            if (SetProperty(ref _selectedAlbum, value))
            {
                SelectedPhoto = null;
                NotifyEditingCommands();
            }
        }
    }
    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                ScanCommand.NotifyCanExecuteChanged();
                CancelScanCommand.NotifyCanExecuteChanged();
                NotifyEditingCommands();
            }
        }
    }
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }
    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand CancelScanCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IRelayCommand ToggleBubblesCommand { get; }
    public IAsyncRelayCommand MergeCommand { get; }
    public bool AreBubblesOpen { get => _areBubblesOpen; set => SetProperty(ref _areBubblesOpen, value); }
    public double ScanProgressValue { get => _scanProgressValue; private set => SetProperty(ref _scanProgressValue, value); }
    public string RecognitionEngineText => _service.ModelIdentity.Engine == FaceRecognitionEngineKind.ArcFaceR100
        ? "当前扫描模型：ArcFace R100（用户提供）"
        : "当前扫描模型：YuNet 检测 + SFace 识别";
    public string RecognitionDetailsText =>
        $"版本：{_service.ModelIdentity.ModelVersion} · 匹配阈值：{_service.ModelIdentity.MatchThreshold:0.00}";
    public string SummaryText
    {
        get
        {
            var facePhotos = Albums.SelectMany(album => album.PhotoPaths)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
            return _detectedFaceCount > 0
                ? $"{Albums.Count} 个人物 · {facePhotos} 张含人脸照片 · 本次检测 {_detectedFaceCount} 张人脸"
                : $"{Albums.Count} 个人物 · {facePhotos} 张含人脸照片";
        }
    }

    public async Task InitializeAsync()
    {
        var snapshot = await _service.LoadAsync().ConfigureAwait(true);
        ReplaceAlbums(snapshot);
        StatusText = Albums.Count == 0 ? "点击扫描，在本机建立人物相册" : $"已保存 {Albums.Count} 个人物相册";
        RefreshRecognitionStatus();
    }

    private Task ScanAsync() => ScanPathsAsync(_pathProvider());

    public async Task ScanPathsAsync(IEnumerable<string> sourcePaths)
    {
        if (IsScanning || IsEditing) return;
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        IsScanning = true;
        ScanProgressValue = 0;
        _detectedFaceCount = 0;
        StatusText = "正在本机检测人脸…";
        try
        {
            var paths = sourcePaths.Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var progress = new Progress<PeopleScanProgress>(report =>
            {
                ScanProgressValue = report.Total == 0 ? 100 : report.Processed * 100d / report.Total;
                _detectedFaceCount = report.DetectedFaces;
                ReconcileProgressAlbums(report.Albums);
                StatusText = $"正在检测人脸… {report.Processed}/{report.Total}（{ScanProgressValue:0}%）";
                OnPropertyChanged(nameof(SummaryText));
            });
            var snapshot = await _service.ScanAsync(paths, progress, _scanCancellation.Token).ConfigureAwait(true);
            ReplaceAlbums(snapshot);
            StatusText = Albums.Count == 0 ? "没有检测到清晰人脸" : $"已建立 {Albums.Count} 个人物相册";
            ScanProgressValue = 100;
        }
        catch (OperationCanceledException)
        {
            StatusText = "人物扫描已取消";
        }
        finally
        {
            IsScanning = false;
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    public void RefreshRecognitionStatus()
    {
        OnPropertyChanged(nameof(RecognitionEngineText));
        OnPropertyChanged(nameof(RecognitionDetailsText));
        OnPropertyChanged(nameof(SummaryText));
    }

    private bool CanMerge() => !IsScanning && !IsEditing && SelectedAlbum is not null && Albums.Count >= 2;
    private bool CanEditPhoto() => !IsScanning && !IsEditing && SelectedAlbum is not null && SelectedPhoto is not null
        && SelectedAlbum.PhotoPaths.Contains(SelectedPhoto.Path);
    private void NotifyEditingCommands()
    {
        MergeCommand.NotifyCanExecuteChanged();
        RemovePhotoCommand.NotifyCanExecuteChanged();
        SplitPhotoCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        ScanCommand.NotifyCanExecuteChanged();
    }
    private async Task EditPhotoAsync(bool split)
    {
        if (!CanEditPhoto()) return;
        var albumId = SelectedAlbum!.Id;
        var path = SelectedPhoto!.Path;
        IsEditing = true;
        try
        {
            if (split) albumId = await _service.SplitAsync(albumId, [path], string.Empty, default);
            else await _service.RemovePhotoAsync(albumId, path, default);
            await RefreshAlbumsAsync();
            SelectedAlbum = Albums.FirstOrDefault(album => album.Id == albumId);
            StatusText = split ? "已将照片拆分到新人物，可撤销。原照片保留。" : "已移出误识别照片，可撤销。原照片保留。";
        }
        catch (Exception exception) { StatusText = $"人物整理失败：{exception.Message}"; }
        finally { IsEditing = false; }
    }
    private async Task UndoAsync()
    {
        IsEditing = true;
        try
        {
            var restored = await _service.UndoAsync();
            await RefreshAlbumsAsync();
            StatusText = restored ? "已撤销最近一次人物整理。" : "暂无可撤销的人物整理。";
        }
        catch (Exception exception) { StatusText = $"撤销失败：{exception.Message}"; }
        finally { IsEditing = false; }
    }

    private async Task MergeSelectedAsync()
    {
        var source = SelectedAlbum;
        if (source is null) return;

        var candidates = Albums.Where(album => !ReferenceEquals(album, source)).ToArray();
        if (candidates.Length == 0) return;

        var target = _mergeTargetPicker(candidates);
        if (target is null) return;

        var sourceName = source.Name;
        var targetName = target.Name;
        IsEditing = true;
        try
        {
        await _service.MergeAsync(target.Id, source.Id, default).ConfigureAwait(true);
        await RefreshAlbumsAsync().ConfigureAwait(true);

        SelectedAlbum = Albums.FirstOrDefault(album => album.Id == target.Id);
        StatusText = string.IsNullOrWhiteSpace(sourceName)
            ? $"已合并到「{targetName}」"
            : $"已将「{sourceName}」合并到「{targetName}」";
        }
        catch (Exception exception) { StatusText = $"合并失败：{exception.Message}"; }
        finally { IsEditing = false; }
    }

    private async Task RefreshAlbumsAsync()
    {
        var snapshot = await _service.LoadAsync().ConfigureAwait(true);
        ReplaceAlbums(snapshot);
    }

    private static PersonAlbumItemViewModel? ShowMergeDialog(IReadOnlyList<PersonAlbumItemViewModel> candidates)
    {
        var dialog = new MergePersonDialog(candidates)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.SelectedTarget : null;
    }

    private void CancelScan() => _scanCancellation?.Cancel();

    private void ReconcileProgressAlbums(IReadOnlyList<PeopleScanAlbumProgress> progressAlbums)
    {
        var reportedIds = progressAlbums.Select(album => album.Id).ToHashSet(StringComparer.Ordinal);
        for (var index = Albums.Count - 1; index >= 0; index--)
            if (!reportedIds.Contains(Albums[index].Id))
                Albums.RemoveAt(index);

        for (var index = 0; index < progressAlbums.Count; index++)
        {
            var progress = progressAlbums[index];
            var existing = Albums.FirstOrDefault(album => album.Id == progress.Id);
            if (existing is null)
            {
                existing = new PersonAlbumItemViewModel(
                    new PersonAlbum
                    {
                        Id = progress.Id,
                        Name = progress.Name,
                        CoverPath = progress.CoverPath,
                        PhotoPaths = progress.PhotoPaths.ToList()
                    },
                    _service,
                    item =>
                    {
                        SelectedAlbum = item;
                        AreBubblesOpen = false;
                    });
                Albums.Insert(Math.Min(index, Albums.Count), existing);
            }
            else
            {
                existing.UpdateFromProgress(progress);
                var currentIndex = Albums.IndexOf(existing);
                if (currentIndex != index)
                    Albums.Move(currentIndex, index);
            }
        }
        OnPropertyChanged(nameof(SummaryText));
    }

    private void ReplaceAlbums(PeopleAlbumSnapshot snapshot)
    {
        CanUndo = snapshot.Undo is not null;
        var selectedId = SelectedAlbum?.Id;
        Albums.Clear();
        foreach (var album in snapshot.Albums.OrderBy(album => album.Name, StringComparer.CurrentCultureIgnoreCase))
            Albums.Add(new PersonAlbumItemViewModel(album, _service, item =>
            {
                SelectedAlbum = item;
                AreBubblesOpen = false;
            }));
        SelectedAlbum = Albums.FirstOrDefault(album => album.Id == selectedId);
        OnPropertyChanged(nameof(SummaryText));
    }
}

/// <summary>人物相册列表中的封面、名称和照片数量。</summary>
public sealed class PersonAlbumItemViewModel : ObservableObject
{
    private readonly PeopleAlbumService _service;
    private string _name;
    private string _coverPath;

    public PersonAlbumItemViewModel(PersonAlbum album, PeopleAlbumService service, Action<PersonAlbumItemViewModel> select)
    {
        _service = service;
        Id = album.Id;
        _name = album.Name;
        _coverPath = album.CoverPath;
        PhotoPaths = album.PhotoPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Photos = new ObservableCollection<PersonPhotoViewModel>(
            PhotoPaths.Select(path => new PersonPhotoViewModel(path)));
        SelectCommand = new RelayCommand(() => select(this));
        SaveNameCommand = new AsyncRelayCommand(() => _service.RenameAsync(Id, Name, default));
    }

    public string Id { get; }
    public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
    public string CoverPath => _coverPath;
    public HashSet<string> PhotoPaths { get; }
    public ObservableCollection<PersonPhotoViewModel> Photos { get; }
    public int PhotoCount => PhotoPaths.Count;
    public IRelayCommand SelectCommand { get; }
    public IAsyncRelayCommand SaveNameCommand { get; }

    public void UpdateFromProgress(PeopleScanAlbumProgress progress)
    {
        _coverPath = progress.CoverPath;
        OnPropertyChanged(nameof(CoverPath));
        PhotoPaths.Clear();
        PhotoPaths.UnionWith(progress.PhotoPaths);
        OnPropertyChanged(nameof(PhotoCount));
        RebuildPhotos();
    }

    private void RebuildPhotos()
    {
        var existing = Photos.ToDictionary(photo => photo.Path, StringComparer.OrdinalIgnoreCase);
        Photos.Clear();
        foreach (var path in PhotoPaths)
        {
            if (!existing.TryGetValue(path, out var photo))
                photo = new PersonPhotoViewModel(path);
            Photos.Add(photo);
        }
    }
}

/// <summary>
/// A single photo belonging to a person album. The <see cref="Thumbnail"/> is
/// decoded lazily and off the UI thread via <see cref="EnsureThumbnailLoaded"/>,
/// which is triggered from the view when the virtualized tile is realized, so a
/// person with hundreds of photos only decodes the tiles currently on screen.
/// </summary>
/// <summary>人物相册中的单张照片及其缩略图加载状态。</summary>
public sealed class PersonPhotoViewModel : ObservableObject
{
    private static readonly SemaphoreSlim ThumbnailGate = new(PreviewLoadingPolicy.ThumbnailConcurrency);
    private ImageSource? _thumbnail;
    private int _loadState;

    public PersonPhotoViewModel(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
    }

    public string Path { get; }
    public string Name { get; }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        private set
        {
            if (SetProperty(ref _thumbnail, value))
                OnPropertyChanged(nameof(HasThumbnail));
        }
    }

    public bool HasThumbnail => Thumbnail is not null;

    public void EnsureThumbnailLoaded()
    {
        if (Thumbnail is not null || Interlocked.CompareExchange(ref _loadState, 1, 0) != 0)
        {
            return;
        }

        _ = LoadCoreAsync();
    }

    private async Task LoadCoreAsync()
    {
        try
        {
            await ThumbnailGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Thumbnail is not null) return;
                var image = await Task.Run(() => LoadThumbnail(Path, 280)).ConfigureAwait(false);
                if (image is null) return;
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher is null) return;
                await dispatcher.InvokeAsync(() => Thumbnail = image);
            }
            finally
            {
                ThumbnailGate.Release();
            }
        }
        catch
        {
            // A single undecodable file must never fail the batch; keep the placeholder.
        }
        finally
        {
            _loadState = 2;
        }
    }

    private static ImageSource? LoadThumbnail(string path, int width)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = width;
            image.UriSource = new Uri(path, UriKind.Absolute);
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
