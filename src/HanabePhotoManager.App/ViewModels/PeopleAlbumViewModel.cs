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
    private CancellationTokenSource? _scanCancellation;
    private double _scanProgressValue;
    private int _detectedFaceCount;

    public PeopleAlbumViewModel(PeopleAlbumService service, Func<IEnumerable<string>> pathProvider)
    {
        _service = service;
        _pathProvider = pathProvider;
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
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
            if (SetProperty(ref _isScanning, value))
            {
                ScanCommand.NotifyCanExecuteChanged();
                CancelScanCommand.NotifyCanExecuteChanged();
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
        if (IsScanning) return;
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
        SelectCommand = new RelayCommand(() => select(this));
        SaveNameCommand = new AsyncRelayCommand(() => _service.RenameAsync(Id, Name, default));
    }

    public string Id { get; }
    public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
    public string CoverPath => _coverPath;
    public HashSet<string> PhotoPaths { get; }
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
    }
}
