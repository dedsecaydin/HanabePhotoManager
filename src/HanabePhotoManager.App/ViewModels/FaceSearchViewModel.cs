using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Services;
using Microsoft.Win32;

namespace HanabePhotoManager.App.ViewModels;

public sealed class FaceSearchViewModel : ObservableObject
{
    private readonly FaceSearchService _service = new();
    private readonly Func<string> _libraryRoot;
    private CancellationTokenSource? _cancellation;
    private FaceReference? _reference;
    private string _referencePath = string.Empty;
    private ImageSource? _referenceImage;
    private string _statusText = "放入一张清晰的人脸照片，然后主动开始查找。";
    private string _progressText = "尚未开始";
    private double _progressValue;
    private double _minimumSimilarity = 0.42;
    private string _selectedSearchScope = "全图库";
    private string _searchFolder = string.Empty;
    private bool _isBusy;

    public FaceSearchViewModel(Func<string> libraryRoot)
    {
        _libraryRoot = libraryRoot;
        ChooseReferenceCommand = new AsyncRelayCommand(ChooseReferenceAsync, () => !IsBusy);
        ChooseSearchFolderCommand = new RelayCommand(ChooseSearchFolder, () => !IsBusy);
        StartSearchCommand = new AsyncRelayCommand(StartSearchAsync, CanStartSearch);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        ClearCommand = new RelayCommand(Clear, () => !IsBusy && HasReference);
        OpenResultCommand = new RelayCommand<FaceSearchResultViewModel>(OpenResult);
        OpenResultFolderCommand = new RelayCommand<FaceSearchResultViewModel>(OpenResultFolder);
    }

    public ObservableCollection<FaceSearchResultViewModel> Results { get; } = [];
    public IAsyncRelayCommand ChooseReferenceCommand { get; }
    public IRelayCommand ChooseSearchFolderCommand { get; }
    public IAsyncRelayCommand StartSearchCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand<FaceSearchResultViewModel> OpenResultCommand { get; }
    public IRelayCommand<FaceSearchResultViewModel> OpenResultFolderCommand { get; }

    public string ReferencePath
    {
        get => _referencePath;
        private set => SetProperty(ref _referencePath, value);
    }

    public ImageSource? ReferenceImage
    {
        get => _referenceImage;
        private set => SetProperty(ref _referenceImage, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public double MinimumSimilarity
    {
        get => _minimumSimilarity;
        set
        {
            if (SetProperty(ref _minimumSimilarity, Math.Clamp(value, 0.30, 0.72)))
            {
                OnPropertyChanged(nameof(SimilarityLabel));
            }
        }
    }

    public string SimilarityLabel => $"{MinimumSimilarity:P0}";

    public IReadOnlyList<string> SearchScopes { get; } = ["全图库", "指定文件夹"];

    public string SelectedSearchScope
    {
        get => _selectedSearchScope;
        set
        {
            if (SetProperty(ref _selectedSearchScope, value))
            {
                OnPropertyChanged(nameof(IsCustomSearchScope));
                OnPropertyChanged(nameof(SearchScopeSummary));
                StartSearchCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsCustomSearchScope => SelectedSearchScope == "指定文件夹";

    public string SearchFolder
    {
        get => _searchFolder;
        private set
        {
            if (SetProperty(ref _searchFolder, value))
            {
                OnPropertyChanged(nameof(SearchScopeSummary));
                StartSearchCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SearchScopeSummary => IsCustomSearchScope
        ? (string.IsNullOrWhiteSpace(SearchFolder) ? "尚未选择查找文件夹" : SearchFolder)
        : "整个本机照片库";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                NotifyCommands();
            }
        }
    }

    public bool IsIdle => !IsBusy;
    public bool HasReference => _reference is not null;
    public bool HasResults => Results.Count > 0;
    public string ResultSummary => HasResults ? $"找到 {Results.Count:N0} 张相似照片" : "还没有查找结果";

    public async Task SetReferenceAsync(string path)
    {
        if (IsBusy || !File.Exists(path)) return;
        var extension = Path.GetExtension(path);
        if (!new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff" }
                .Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            StatusText = "参考图请选择 JPG、PNG、WebP、BMP 或 TIFF。";
            return;
        }

        IsBusy = true;
        ProgressValue = 0;
        ProgressText = "正在确认参考图中的人脸…";
        StatusText = "正在本机检测人脸，不会上传照片。";
        _cancellation = new CancellationTokenSource();
        try
        {
            ReferenceImage = LoadThumbnail(path, 420);
            _reference = await _service.CreateReferenceAsync(path, _cancellation.Token);
            ReferencePath = path;
            Results.Clear();
            NotifyResultState();
            ProgressValue = 100;
            ProgressText = "参考人脸已就绪";
            StatusText = "已检测到人脸。调整相似度后点击“开始查找”，应用才会扫描图库。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已停止。";
        }
        catch (Exception ex)
        {
            _reference = null;
            ReferencePath = string.Empty;
            ReferenceImage = null;
            ProgressText = "参考图不可用";
            StatusText = ex.Message;
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            IsBusy = false;
            OnPropertyChanged(nameof(HasReference));
            NotifyCommands();
        }
    }

    public void Cancel() => _cancellation?.Cancel();

    public void NotifyLibraryRootChanged() => StartSearchCommand.NotifyCanExecuteChanged();

    private async Task ChooseReferenceAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择一张参考人脸照片",
            Filter = "照片|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tif;*.tiff|所有文件|*.*",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true) await SetReferenceAsync(dialog.FileName);
    }

    private void ChooseSearchFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择人物查找范围",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            SearchFolder = dialog.FolderName;
            SelectedSearchScope = "指定文件夹";
        }
    }

    private string EffectiveSearchRoot => IsCustomSearchScope ? SearchFolder : _libraryRoot();

    private bool CanStartSearch() => !IsBusy && HasReference && Directory.Exists(EffectiveSearchRoot);

    private async Task StartSearchAsync()
    {
        if (_reference is null || !CanStartSearch()) return;
        IsBusy = true;
        Results.Clear();
        NotifyResultState();
        ProgressValue = 0;
        ProgressText = "正在读取图库清单…";
        StatusText = "首次查找会建立人脸特征缓存；RAW 和视频不会参与分析。";
        _cancellation = new CancellationTokenSource();
        var progress = new Progress<FaceSearchProgress>(value =>
        {
            ProgressValue = value.Total == 0 ? 0 : value.Processed * 100d / value.Total;
            ProgressText = $"{value.Processed:N0} / {value.Total:N0} · 暂时命中 {value.Matches:N0} 张";
            StatusText = value.FromCache
                ? $"正在比对缓存 · {value.CurrentFile}"
                : $"正在检测人脸 · {value.CurrentFile}";
        });

        try
        {
            var matches = await _service.SearchAsync(
                _reference, EffectiveSearchRoot, MinimumSimilarity, progress, _cancellation.Token);
            foreach (var match in matches)
            {
                Results.Add(new FaceSearchResultViewModel(match));
            }

            NotifyResultState();
            ProgressValue = 100;
            ProgressText = $"完成 · 找到 {matches.Count:N0} 张";
            StatusText = matches.Count == 0
                ? "没有达到当前相似度的照片。可以把相似度稍微调低后重试。"
                : "已按相似度从高到低排列；双击照片可直接打开。";
            _ = LoadResultThumbnailsAsync(Results.ToArray(), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            ProgressText = "查找已停止";
            StatusText = "任务已安全停止，已生成的本地缓存仍会在下次使用。";
        }
        catch (Exception ex)
        {
            ProgressText = "查找失败";
            StatusText = ex.Message;
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            IsBusy = false;
        }
    }

    private static async Task LoadResultThumbnailsAsync(
        IReadOnlyList<FaceSearchResultViewModel> results,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(4);
        var tasks = results.Select(async result =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var image = await Task.Run(() => LoadThumbnail(result.Path, 360), cancellationToken);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => result.Thumbnail = image);
            }
            catch { }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);
    }

    private void Clear()
    {
        _reference = null;
        ReferencePath = string.Empty;
        ReferenceImage = null;
        Results.Clear();
        ProgressValue = 0;
        ProgressText = "尚未开始";
        StatusText = "放入一张清晰的人脸照片，然后主动开始查找。";
        OnPropertyChanged(nameof(HasReference));
        NotifyResultState();
        NotifyCommands();
    }

    private static void OpenResult(FaceSearchResultViewModel? item)
    {
        if (item is null || !File.Exists(item.Path)) return;
        Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
    }

    private static void OpenResultFolder(FaceSearchResultViewModel? item)
    {
        if (item is null || !File.Exists(item.Path)) return;
        Process.Start("explorer.exe", $"/select,\"{item.Path}\"");
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
        catch { return null; }
    }

    private void NotifyCommands()
    {
        ChooseReferenceCommand.NotifyCanExecuteChanged();
        ChooseSearchFolderCommand.NotifyCanExecuteChanged();
        StartSearchCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private void NotifyResultState()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ResultSummary));
    }
}

public sealed class FaceSearchResultViewModel : ObservableObject
{
    private ImageSource? _thumbnail;

    public FaceSearchResultViewModel(FaceSearchMatch match)
    {
        Path = match.Path;
        Similarity = match.Similarity;
        FacesInImage = match.FacesInImage;
    }

    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
    public double Similarity { get; }
    public string SimilarityText => $"相似 {Similarity:P0}";
    public int FacesInImage { get; }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }
}
