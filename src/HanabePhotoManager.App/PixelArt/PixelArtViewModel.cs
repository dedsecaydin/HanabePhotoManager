using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HanabePhotoManager.App.PixelArt;

public sealed class PixelArtViewModel : ObservableObject
{
    private string _sourceImagePath = string.Empty;
    private ImageSource? _sourceImage;
    private ImageSource? _pixelArtImage;
    private int _selectedSize = 128;
    private bool _isCustom;
    private string _customSizeText = "128";
    private string _statusText = "选择一张图片，然后生成像素画。";
    private bool _isBusy;
    private int _pixelWidth;
    private int _pixelHeight;
    private BitmapSource? _grid;

    public PixelArtViewModel()
    {
        GenerateCommand = new RelayCommand(Generate, () => CanGenerate);
    }

    public IReadOnlyList<int> PresetSizes { get; } = [64, 128, 256];

    public IRelayCommand GenerateCommand { get; }

    public string SourceImagePath { get => _sourceImagePath; private set => SetProperty(ref _sourceImagePath, value); }
    public ImageSource? SourceImage { get => _sourceImage; private set => SetProperty(ref _sourceImage, value); }
    public ImageSource? PixelArtImage { get => _pixelArtImage; private set => SetProperty(ref _pixelArtImage, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public int PixelWidth { get => _pixelWidth; private set => SetProperty(ref _pixelWidth, value); }
    public int PixelHeight { get => _pixelHeight; private set => SetProperty(ref _pixelHeight, value); }

    /// <summary>当前选中的预设尺寸（默认 128）。</summary>
    public int SelectedSize { get => _selectedSize; private set => SetProperty(ref _selectedSize, value); }

    /// <summary>是否选中「自定义」尺寸输入。</summary>
    public bool IsCustom { get => _isCustom; private set => SetProperty(ref _isCustom, value); }

    /// <summary>自定义尺寸输入框文本。</summary>
    public string CustomSizeText { get => _customSizeText; set => SetProperty(ref _customSizeText, value ?? string.Empty); }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) NotifyAvailability(); }
    }

    public bool HasSource => !string.IsNullOrWhiteSpace(SourceImagePath);
    public bool HasResult => _grid is not null;
    public bool CanGenerate => HasSource && !IsBusy;

    public void SetSourceImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = "无法读取所选图片。";
            return;
        }

        try
        {
            SourceImagePath = path;
            SourceImage = PixelArtRenderer.Load(path);
            _grid = null;
            PixelArtImage = null;
            StatusText = $"已选择：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"加载图片失败：{ex.Message}";
        }

        OnPropertyChanged(nameof(HasSource));
        OnPropertyChanged(nameof(HasResult));
        NotifyAvailability();
    }

    /// <summary>选择预设尺寸：同步把数值写回自定义输入框，并退出自定义模式。</summary>
    public void SelectPreset(int size)
    {
        SelectedSize = size;
        IsCustom = false;
        CustomSizeText = size.ToString();
    }

    /// <summary>选中自定义尺寸输入：后续生成使用自定义输入框中的数值。</summary>
    public void SelectCustom()
    {
        IsCustom = true;
    }

    /// <summary>计算当前生效的目标尺寸：自定义模式解析输入（无效回退 128），预设模式用 SelectedSize。</summary>
    public int ResolveEffectiveSize()
    {
        if (IsCustom && TryParseSize(CustomSizeText, out var custom)) return custom;
        if (IsCustom) return 128;
        return SelectedSize;
    }

    public void Generate()
    {
        if (!CanGenerate || SourceImage is not BitmapSource source) return;

        var customInvalid = IsCustom && !TryParseSize(CustomSizeText, out _);
        var size = ResolveEffectiveSize();

        IsBusy = true;
        try
        {
            _grid = PixelArtRenderer.DownscaleToGrid(source, size, out var width, out var height);
            PixelWidth = width;
            PixelHeight = height;
            PixelArtImage = _grid;
            StatusText = customInvalid
                ? $"自定义尺寸无效，已回退到 128；已生成 {width}×{height} 像素画，可导出 PNG。"
                : $"已生成 {width}×{height} 像素画，可导出 PNG。";
        }
        catch (Exception ex)
        {
            StatusText = $"生成失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        OnPropertyChanged(nameof(HasResult));
    }

    public void Export(string outputPath)
    {
        if (_grid is null || string.IsNullOrWhiteSpace(outputPath)) return;

        try
        {
            PixelArtRenderer.Export(_grid, outputPath);
            StatusText = $"已导出：{Path.GetFileName(outputPath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"导出失败：{ex.Message}";
        }
    }

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(CanGenerate));
        GenerateCommand.NotifyCanExecuteChanged();
    }

    private static bool TryParseSize(string? text, out int size)
    {
        if (int.TryParse(text?.Trim(), out size) && size >= 1)
        {
            size = Math.Clamp(size, 8, 4096);
            return true;
        }

        return false;
    }
}
