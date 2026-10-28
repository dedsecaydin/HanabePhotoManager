using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace HanabePhotoManager.App.Video;

public partial class SLog3Window : Window
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".mxf", ".mts", ".m2ts" };
    private readonly SLog3ViewModel _model = new();
    private VideoSourceInfo? _sourceInfo;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _settingPreset;

    public SLog3Window()
    {
        InitializeComponent();
        VideoList.ItemsSource = _model.Jobs;
        DataContext = _model;
        OutputPathBox.Text = _model.OutputDirectory;
        LutPathBox.Text = _model.LutPath ?? "";
        PresetBox.ItemsSource = new[] { "节省空间", "高质量", "自定义" };
        PresetBox.SelectedIndex = (int)_model.Preset;
        GamutBox.ItemsSource = Enum.GetValues<SonySourceGamut>(); GamutBox.SelectedItem = _model.Gamut;
        EncoderBox.ItemsSource = Enum.GetValues<VideoEncoder>(); EncoderBox.SelectedItem = _model.Encoder;
        DepthBox.ItemsSource = new[] { 8, 10 }; DepthBox.SelectedItem = _model.BitDepth;
        QualityModeBox.ItemsSource = Enum.GetValues<VideoQualityMode>(); QualityModeBox.SelectedItem = _model.QualityMode;
        QualityBox.Text = _model.Quality.ToString(); BitrateBox.Text = _model.TargetBitrateMbps.ToString();
        FallbackBox.IsChecked = _model.AllowSoftwareFallback;
        RangeBox.ItemsSource = Enum.GetValues<InputVideoRange>(); RangeBox.SelectedItem = _model.InputRange;
        MatrixBox.ItemsSource = Enum.GetValues<InputYuvMatrix>(); MatrixBox.SelectedItem = _model.InputMatrix;
        VideoList.SelectionChanged += VideoList_SelectionChanged;
        EncoderBox.SelectionChanged += Encoder_SelectionChanged;
        DepthBox.SelectionChanged += Encoder_SelectionChanged;
        PresetBox.SelectionChanged += (_, _) => ApplyPreset();
        QualityModeBox.SelectionChanged += (_, _) => MarkCustomPreset();
        QualityBox.TextChanged += (_, _) => MarkCustomPreset();
        BitrateBox.TextChanged += (_, _) => MarkCustomPreset();
        FallbackBox.Checked += (_, _) => MarkCustomPreset();
        FallbackBox.Unchecked += (_, _) => MarkCustomPreset();
        RangeBox.SelectionChanged += (_, _) => UpdateSourceInfoText();
        MatrixBox.SelectionChanged += (_, _) => UpdateSourceInfoText();
        Loaded += (_, _) => _ = UpdateCapabilityAsync();
        _model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SLog3ViewModel.IsBusy))
                PreviewButton.IsEnabled = ExportButton.IsEnabled = RetryButton.IsEnabled = !_model.IsBusy;
            CancelButton.IsEnabled = _model.IsBusy;
        };
    }

    private void AddVideos_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择 S-Log3 视频", Multiselect = true,
            Filter = "视频|*.mp4;*.mov;*.mxf;*.mts;*.m2ts|所有文件|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        _model.Add(dialog.FileNames.Where(path => Extensions.Contains(Path.GetExtension(path))));
        CountText.Text = $"已选择 {_model.Jobs.Count} 个视频";
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_model.IsBusy) return;
        foreach (VideoJob item in VideoList.SelectedItems.Cast<VideoJob>().ToArray()) _model.Jobs.Remove(item);
        _model.SaveQueue();
        CountText.Text = _model.Jobs.Count == 0 ? "尚未选择视频" : $"已选择 {_model.Jobs.Count} 个视频";
    }

    private void ChooseLut_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择 S-Log3 到 Rec.709 转换 LUT", Filter = "3D LUT|*.cube" };
        if (dialog.ShowDialog(this) == true) LutPathBox.Text = dialog.FileName;
    }
    private void StandardLut_Click(object sender, RoutedEventArgs e) => LutPathBox.Text = "";

    private void ChooseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择还原视频导出目录",
            InitialDirectory = Directory.Exists(OutputPathBox.Text) ? OutputPathBox.Text : @"D:\HanabePhoto" };
        if (dialog.ShowDialog(this) == true) OutputPathBox.Text = dialog.FolderName;
    }

    private VideoProcessingProfile ReadProfile()
    {
        if (_model.Jobs.Count == 0) throw new InvalidOperationException("请先添加视频。");
        if (ConfirmSLog3Box.IsChecked != true)
            throw new InvalidOperationException("请先确认本批素材实际为 S-Log3，并核对输入色域、范围和矩阵。");
        if (!Path.IsPathFullyQualified(OutputPathBox.Text)) throw new InvalidOperationException("请选择有效的导出目录。");
        if (!int.TryParse(QualityBox.Text, out var quality) || !int.TryParse(BitrateBox.Text, out var bitrate))
            throw new InvalidOperationException("质量与目标码率必须是整数。");
        _model.OutputDirectory = OutputPathBox.Text;
        _model.LutPath = string.IsNullOrWhiteSpace(LutPathBox.Text) ? null : LutPathBox.Text;
        _model.Gamut = (SonySourceGamut)GamutBox.SelectedItem;
        _model.Encoder = (VideoEncoder)EncoderBox.SelectedItem;
        _model.BitDepth = (int)DepthBox.SelectedItem;
        _model.QualityMode = (VideoQualityMode)QualityModeBox.SelectedItem;
        _model.Quality = quality; _model.TargetBitrateMbps = bitrate;
        _model.AllowSoftwareFallback = FallbackBox.IsChecked == true;
        _model.Preset = (VideoPreset)PresetBox.SelectedIndex;
        _model.InputRange = (InputVideoRange)RangeBox.SelectedItem;
        _model.InputMatrix = (InputYuvMatrix)MatrixBox.SelectedItem;
        var profile = _model.Snapshot(); profile.Validate(); return profile;
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ReadProfile();
            if (VideoList.SelectedItem is not VideoJob source) throw new InvalidOperationException("请选择列表中的一个视频。");
            var preview = await _model.PreviewAsync(source);
            if (!File.Exists(preview)) return;
            var viewer = new PhotoViewerWindow([source.Source, preview], preview) { Owner = this };
            viewer.Closed += (_, _) => { try { File.Delete(preview); } catch (IOException) { } };
            viewer.Show();
        }
        catch (Exception ex) { _model.ReportError($"预览失败：{ex.Message}"); }
    }

    private async void Export_Click(object sender, RoutedEventArgs e) => await ExportAsync(false);
    private async void Retry_Click(object sender, RoutedEventArgs e) => await ExportAsync(true);
    private async Task ExportAsync(bool failuresOnly)
    {
        try
        {
            if (!failuresOnly) ReadProfile();
            else if (ConfirmSLog3Box.IsChecked != true) throw new InvalidOperationException("请先确认本批素材实际为 S-Log3。");
            await _model.ExportAsync(failuresOnly);
        }
        catch (Exception ex) { _model.ReportError($"导出失败：{ex.Message}"); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => _model.Cancel();
    private async void VideoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VideoList.SelectedItem is not VideoJob job) return;
        try
        {
            var info = await VideoSourceProbe.ReadAsync(job.Source, _lifetime.Token);
            if (VideoList.SelectedItem != job) return;
            _sourceInfo = info;
            UpdateSourceInfoText();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SourceInfoText.Text = $"无法探测视频：{ex.Message}；请手动确认。"; }
    }

    private void UpdateSourceInfoText()
    {
        if (_sourceInfo is null) { SourceInfoText.Text = "未找到可靠文件标记；请手动确认输入范围、矩阵与 S-Log3。"; return; }
        var warnings = new List<string>();
        if ((_sourceInfo.Range == "pc" && RangeBox.SelectedItem is InputVideoRange.Limited) ||
            (_sourceInfo.Range == "tv" && RangeBox.SelectedItem is InputVideoRange.Full))
            warnings.Add("文件范围标记与手选值不同");
        if ((_sourceInfo.Matrix == "bt709" && MatrixBox.SelectedItem is InputYuvMatrix.Bt2020) ||
            (_sourceInfo.Matrix.StartsWith("bt2020", StringComparison.Ordinal) && MatrixBox.SelectedItem is InputYuvMatrix.Bt709))
            warnings.Add("文件矩阵标记与手选值不同");
        if (_sourceInfo.Transfer is "smpte2084" or "arib-std-b67") warnings.Add("文件标为 HDR，请确认实际是否 S-Log3");
        SourceInfoText.Text = $"文件标记：{_sourceInfo.Summary}。" +
            (warnings.Count == 0 ? "标记不保证是 S-Log3，请与相机设置核对。" : string.Join("；", warnings) + "。请手动核对。");
    }

    private void Encoder_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        MarkCustomPreset();
        _ = UpdateCapabilityAsync();
    }

    private void ApplyPreset()
    {
        if (_settingPreset || PresetBox.SelectedIndex < 0) return;
        var selected = (VideoPreset)PresetBox.SelectedIndex;
        _model.Preset = selected;
        if (selected == VideoPreset.Custom) return;
        _settingPreset = true;
        try
        {
            var configured = VideoProcessingPresets.Apply(selected, _model.Snapshot());
            EncoderBox.SelectedItem = configured.Encoder;
            DepthBox.SelectedItem = configured.OutputBitDepth;
            QualityModeBox.SelectedItem = configured.QualityMode;
            QualityBox.Text = configured.Quality.ToString();
            BitrateBox.Text = configured.TargetBitrateMbps.ToString();
            FallbackBox.IsChecked = configured.AllowSoftwareFallback;
        }
        finally { _settingPreset = false; }
    }

    private void MarkCustomPreset()
    {
        if (_settingPreset || PresetBox.SelectedIndex == (int)VideoPreset.Custom) return;
        _settingPreset = true;
        try { PresetBox.SelectedIndex = (int)VideoPreset.Custom; _model.Preset = VideoPreset.Custom; }
        finally { _settingPreset = false; }
    }
    private async Task UpdateCapabilityAsync()
    {
        if (EncoderBox.SelectedItem is not VideoEncoder encoder || DepthBox.SelectedItem is not int depth) return;
        CapabilityText.Text = "正在实际探测编码器…";
        try
        {
            var result = await VideoEncoderProbe.ProbeAsync(encoder, depth, _lifetime.Token);
            if (EncoderBox.SelectedItem is not VideoEncoder selectedEncoder || selectedEncoder != encoder ||
                DepthBox.SelectedItem is not int selectedDepth || selectedDepth != depth) return;
            CapabilityText.Text = result.Available ? $"{encoder} {depth}-bit：{result.Reason}" :
                $"{encoder} {depth}-bit 不可用：{result.Reason}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { CapabilityText.Text = $"编码器探测失败：{ex.Message}"; }
    }
    protected override void OnClosed(EventArgs e) { _model.Cancel(); _lifetime.Cancel(); base.OnClosed(e); }
}
