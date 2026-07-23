using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

public sealed class PhotoViewerViewModel : ObservableObject
{
    private readonly IPhotoDetailMetadataReader _reader;
    private readonly Func<string, int> _ratingReader;
    private readonly Action<string, int> _ratingWriter;
    private readonly IRecycleBinFileService _recycleBin;
    private IReadOnlyList<string> _paths = [];
    private int _index = -1;
    private bool _isOpen;
    private PhotoDetailMetadata _metadata = PhotoDetailMetadata.Empty(string.Empty);
    private BitmapSource? _image;
    private int _rating;
    private double _zoomScale = 1;
    private string _errorText = string.Empty;

    public PhotoViewerViewModel(
        IPhotoDetailMetadataReader? reader = null,
        Func<string, int>? ratingReader = null,
        Action<string, int>? ratingWriter = null,
        IRecycleBinFileService? recycleBin = null)
    {
        _reader = reader ?? new PhotoDetailMetadataReader();
        _ratingReader = ratingReader ?? (path => FileMetaStore.TryGet(path).Rating);
        _ratingWriter = ratingWriter ?? ((path, value) => FileMetaStore.Update(path, meta => meta.Rating = value));
        _recycleBin = recycleBin ?? new RecycleBinFileService();
        PreviousCommand = new RelayCommand(Previous, () => CanPrevious);
        NextCommand = new RelayCommand(Next, () => CanNext);
        CloseCommand = new RelayCommand(Close, () => IsOpen);
        DeleteCommand = new RelayCommand(DeleteCurrent, () => IsOpen && CurrentPath is not null);
        SetRatingCommand = new RelayCommand<string>(value =>
        {
            if (int.TryParse(value, out var rating)) SetRating(rating);
        });
    }

    public event Action<string>? PhotoDeleted;

    public IRelayCommand PreviousCommand { get; }
    public IRelayCommand NextCommand { get; }
    public IRelayCommand CloseCommand { get; }
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand<string> SetRatingCommand { get; }
    public bool IsOpen { get => _isOpen; private set { if (SetProperty(ref _isOpen, value)) CloseCommand.NotifyCanExecuteChanged(); } }
    public string? CurrentPath => _index >= 0 && _index < _paths.Count ? _paths[_index] : null;
    public PhotoDetailMetadata Metadata { get => _metadata; private set => SetProperty(ref _metadata, value); }
    public BitmapSource? Image { get => _image; private set => SetProperty(ref _image, value); }
    public bool CanPrevious => IsOpen && _index > 0;
    public bool CanNext => IsOpen && _index >= 0 && _index < _paths.Count - 1;
    public string PositionText => _index < 0 ? "0 / 0" : $"{_index + 1} / {_paths.Count}";
    public int Rating { get => _rating; private set => SetProperty(ref _rating, value); }
    public double ZoomScale { get => _zoomScale; private set => SetProperty(ref _zoomScale, value); }
    public string ErrorText { get => _errorText; private set => SetProperty(ref _errorText, value); }

    public void Open(IEnumerable<string> paths, string selectedPath)
    {
        _paths = paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _index = Array.FindIndex(_paths.ToArray(), path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase));
        if (_index < 0 && _paths.Count > 0) _index = 0;
        IsOpen = _index >= 0;
        RefreshCurrent();
    }

    public void Previous() { if (CanPrevious) { _index--; RefreshCurrent(); } }
    public void Next() { if (CanNext) { _index++; RefreshCurrent(); } }
    public void SetRating(int value)
    {
        if (CurrentPath is not { } path) return;
        Rating = Math.Clamp(value, 0, 5);
        _ratingWriter(path, Rating);
    }
    public void AdjustZoom(int direction)
    {
        if (direction == 0) return;
        ZoomScale = Math.Clamp(ZoomScale * (direction > 0 ? 1.12 : 1 / 1.12), 0.25, 8);
    }
    public void ResetZoom() => ZoomScale = 1;

    public void DeleteCurrent()
    {
        if (CurrentPath is not { } path) return;
        ErrorText = string.Empty;
        try { _recycleBin.MoveToRecycleBin(path); }
        catch (Exception ex) { ErrorText = $"无法移入回收站：{ex.Message}"; return; }

        var remaining = _paths.Where(item => !string.Equals(item, path, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (remaining.Length == 0)
        {
            _paths = remaining;
            _index = -1;
            PhotoDeleted?.Invoke(path);
            Close();
            return;
        }
        _paths = remaining;
        _index = Math.Min(_index, remaining.Length - 1);
        PhotoDeleted?.Invoke(path);
        RefreshCurrent();
    }
    public void Close()
    {
        IsOpen = false;
        Image = null;
        NotifyNavigation();
    }

    private void RefreshCurrent()
    {
        if (CurrentPath is not { } path) return;
        ResetZoom();
        Metadata = _reader.Read(path);
        Rating = Math.Clamp(_ratingReader(path), 0, 5);
        ErrorText = string.Empty;
        Image = Load(path);
        OnPropertyChanged(nameof(CurrentPath));
        OnPropertyChanged(nameof(PositionText));
        NotifyNavigation();
    }

    private void NotifyNavigation()
    {
        OnPropertyChanged(nameof(CanPrevious));
        OnPropertyChanged(nameof(CanNext));
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private static BitmapSource? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }
}
