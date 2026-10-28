using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Security.Cryptography;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.Video;

internal sealed class VideoJob : INotifyPropertyChanged
{
    private string _state = "待处理";
    private string _detail = "";
    public VideoJob(string source) => Source = source;
    public string Source { get; }
    public string Name => Path.GetFileName(Source);
    public string State { get => _state; set { _state = value; PropertyChanged?.Invoke(this, new(nameof(State))); } }
    public string Detail { get => _detail; set { _detail = value; PropertyChanged?.Invoke(this, new(nameof(Detail))); } }
    public VideoProcessingProfile? FrozenProfile { get; set; }
    public string? FrozenOutputDirectory { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
}

internal sealed class SLog3ViewModel : INotifyPropertyChanged
{
    private readonly string _dataRoot;
    private string PreferencesPath => Path.Combine(_dataRoot, "video", "preferences.json");
    private string QueuePath => Path.Combine(_dataRoot, "video", "queue.json");
    private static readonly object QueueFileGate = new();
    private CancellationTokenSource? _cancel;
    private string _status = "等待添加视频";
    private double _progress;
    public ObservableCollection<VideoJob> Jobs { get; } = [];
    public string Status { get => _status; private set { _status = value; Changed(); } }
    public double Progress { get => _progress; private set { _progress = value; Changed(); } }
    public bool IsBusy => _cancel is not null;
    public string OutputDirectory { get; set; } = Path.Combine(@"D:\HanabePhoto", "SLog3Exports");
    public string? LutPath { get; set; }
    public SonySourceGamut Gamut { get; set; } = SonySourceGamut.SGamut3Cine;
    public VideoEncoder Encoder { get; set; } = VideoEncoder.H265Software;
    public int BitDepth { get; set; } = 10;
    public VideoQualityMode QualityMode { get; set; } = VideoQualityMode.ConstantQuality;
    public int Quality { get; set; } = 20;
    public int TargetBitrateMbps { get; set; } = 20;
    public bool AllowSoftwareFallback { get; set; }
    public VideoPreset Preset { get; set; } = VideoPreset.Custom;
    public InputVideoRange InputRange { get; set; } = InputVideoRange.Full;
    public InputYuvMatrix InputMatrix { get; set; } = InputYuvMatrix.Bt709;
    public event PropertyChangedEventHandler? PropertyChanged;

    public SLog3ViewModel(string? dataRoot = null)
    {
        _dataRoot = dataRoot ?? AppDataPaths.ProcessingRoot;
        try
        {
            if (File.Exists(PreferencesPath))
            {
                var saved = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PreferencesPath));
                if (saved is not null)
                {
                    OutputDirectory = saved.OutputDirectory; Gamut = saved.Gamut; Encoder = saved.Encoder;
                    BitDepth = saved.BitDepth; QualityMode = saved.QualityMode; Quality = saved.Quality;
                    TargetBitrateMbps = saved.TargetBitrateMbps; AllowSoftwareFallback = saved.AllowSoftwareFallback;
                    InputRange = saved.InputRange; InputMatrix = saved.InputMatrix;
                    LutPath = saved.LutPath;
                    Preset = saved.Preset ?? VideoPreset.Custom;
                }
            }
            if (File.Exists(QueuePath))
            {
                foreach (var saved in JsonSerializer.Deserialize<List<JobSnapshot>>(File.ReadAllText(QueuePath)) ?? [])
                {
                    var job = new VideoJob(saved.Source) { State = saved.State, Detail = saved.Detail,
                        FrozenProfile = saved.Profile, FrozenOutputDirectory = saved.OutputDirectory };
                    if (job.State is "处理中" or "待处理" or "已取消")
                    { job.State = "失败"; job.Detail = "上次任务未完成，可重试。"; }
                    Jobs.Add(job);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { Status = "视频偏好读取失败，已使用默认值。"; }
    }

    public void SavePreferences()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
        var temporary = PreferencesPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new Preferences(OutputDirectory, Gamut, Encoder, BitDepth,
            QualityMode, Quality, TargetBitrateMbps, AllowSoftwareFallback, InputRange, InputMatrix, LutPath, Preset)));
        File.Move(temporary, PreferencesPath, true);
    }

    public void Add(IEnumerable<string> sources)
    {
        foreach (var source in sources.Where(File.Exists))
            if (!Jobs.Any(job => job.Source.Equals(source, StringComparison.OrdinalIgnoreCase))) Jobs.Add(new VideoJob(source));
        Status = $"已选择 {Jobs.Count} 个视频";
        SaveQueue();
    }

    public void SaveQueue()
    {
        lock (QueueFileGate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(QueuePath)!);
            var temporary = QueuePath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(Jobs.Select(job =>
                    new JobSnapshot(job.Source, job.State, job.Detail, job.FrozenProfile, job.FrozenOutputDirectory)).ToArray()));
                File.Move(temporary, QueuePath, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public VideoProcessingProfile Snapshot() => new(Gamut, LutPath, Encoder, BitDepth,
        QualityMode, Quality, TargetBitrateMbps, AllowSoftwareFallback, InputRange, InputMatrix);

    private VideoProcessingProfile FreezeLut(VideoProcessingProfile profile)
    {
        profile.Validate();
        if (profile.CustomLutPath is not { Length: > 0 } source) return profile;
        var bytes = File.ReadAllBytes(source);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var folder = Path.Combine(_dataRoot, "video", "luts");
        Directory.CreateDirectory(folder);
        var frozen = Path.Combine(folder, $"{hash}.cube");
        if (!File.Exists(frozen))
        {
            var temporary = Path.Combine(folder, $".{Guid.NewGuid():N}.cube");
            try
            {
                File.WriteAllBytes(temporary, bytes);
                CubeLut.Validate(temporary);
                try { File.Move(temporary, frozen); }
                catch (IOException) when (File.Exists(frozen)) { }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        if (!SHA256.HashData(File.ReadAllBytes(frozen)).AsSpan().SequenceEqual(Convert.FromHexString(hash)))
            throw new InvalidDataException("冻结的 LUT 文件校验失败。");
        return profile with { CustomLutPath = frozen };
    }

    public async Task<string> PreviewAsync(VideoJob selected)
    {
        var profile = FreezeLut(Snapshot());
        var previewDirectory = Path.Combine(_dataRoot, "video", "preview");
        Directory.CreateDirectory(previewDirectory);
        var output = Path.Combine(previewDirectory, $"preview-{Guid.NewGuid():N}.mp4");
        await RunAsync(async token =>
        {
            Status = "正在生成 10 秒预览";
            await SLog3Transcoder.ConvertAsync(selected.Source, output, profile, true, token);
            Status = "预览已生成";
        });
        return output;
    }

    public async Task ExportAsync(bool failuresOnly = false)
    {
        var profile = Snapshot();
        if (!failuresOnly) profile = FreezeLut(profile);
        var selected = Jobs.Where(job => failuresOnly ? job.State == "失败" : job.State != "已完成").ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("没有待处理视频。");
        if (failuresOnly && selected.Any(job => job.FrozenProfile is null)) profile = FreezeLut(profile);
        if (!failuresOnly) SavePreferences();
        await RunAsync(async token =>
        {
            var done = 0;
            foreach (var job in selected)
            {
                token.ThrowIfCancellationRequested();
                if (!failuresOnly || job.FrozenProfile is null)
                { job.FrozenProfile = profile; job.FrozenOutputDirectory = OutputDirectory; }
                job.State = "处理中"; job.Detail = "";
                SaveQueue();
                Status = $"正在导出 {done + 1}/{selected.Length}：{job.Name}";
                try
                {
                    var completedBeforeThisJob = done;
                    var progress = new Progress<double>(percent =>
                        Progress = 100.0 * (completedBeforeThisJob + percent / 100.0) / selected.Length);
                    var warnings = new List<string>();
                    var events = new InlineProgress<string>(message =>
                    { warnings.Add(message); job.Detail = string.Join("；", warnings); SaveQueue(); });
                    var output = await SLog3Transcoder.ExportAsync(job.Source, job.FrozenOutputDirectory!, job.FrozenProfile!, token, progress, events);
                    job.State = "已完成"; job.Detail = warnings.Count == 0 ? output : $"{output}；{string.Join("；", warnings)}";
                }
                catch (OperationCanceledException) { job.State = "已取消"; SaveQueue(); throw; }
                catch (Exception ex) { job.State = "失败"; job.Detail = ex.Message; }
                SaveQueue();
                done++; Progress = 100.0 * done / selected.Length;
            }
            Status = $"批次完成：{selected.Count(job => job.State == "已完成")} 成功，{selected.Count(job => job.State == "失败")} 失败";
        });
    }

    public void Cancel() => _cancel?.Cancel();
    public void ReportError(string message) => Status = message;

    private async Task RunAsync(Func<CancellationToken, Task> run)
    {
        if (IsBusy) throw new InvalidOperationException("已有视频任务运行。");
        _cancel = new CancellationTokenSource(); Changed(nameof(IsBusy)); Progress = 0;
        try { await run(_cancel.Token); }
        catch (OperationCanceledException) { Status = "任务已取消，未完成输出已清理。"; }
        finally { _cancel.Dispose(); _cancel = null; Changed(nameof(IsBusy)); }
    }

    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    private sealed record Preferences(string OutputDirectory, SonySourceGamut Gamut, VideoEncoder Encoder,
        int BitDepth, VideoQualityMode QualityMode, int Quality, int TargetBitrateMbps, bool AllowSoftwareFallback,
        InputVideoRange InputRange, InputYuvMatrix InputMatrix, string? LutPath, VideoPreset? Preset);
    private sealed record JobSnapshot(string Source, string State, string Detail,
        VideoProcessingProfile? Profile, string? OutputDirectory);
    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
