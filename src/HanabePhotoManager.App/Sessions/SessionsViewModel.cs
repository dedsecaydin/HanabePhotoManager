using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.Core.Sessions;
namespace HanabePhotoManager.App.Sessions;
public sealed partial class SessionsViewModel : ObservableObject
{
    private readonly ISessionStore _store; private readonly ICaptureTimeReader _reader; private readonly string[] _paths;
    private SessionDocument _document = SessionDocument.Empty; private IReadOnlyList<SessionGroup> _groups = []; private CancellationTokenSource? _cancellation;
    public ObservableCollection<SessionGroup> Groups { get; private set; } = [];
    public ObservableCollection<CaptureMember> Members { get; private set; } = [];
    [ObservableProperty] private SessionGroup? selectedGroup;
    [ObservableProperty] private CaptureMember? selectedMember;
    [ObservableProperty] private double thresholdMinutes = 30;
    [ObservableProperty] private string sort = "时间";
    [ObservableProperty] private string status = "尚未读取；预览不会修改素材。";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private double progress;
    [ObservableProperty] private string camera = "";
    [ObservableProperty] private double correctionMinutes;
    public string[] SortChoices { get; } = ["时间", "日期", "文件名"];
    public ObservableCollection<string> Cameras { get; } = [];
    private int _detectionGeneration;
    private readonly Dictionary<string, double> _corrections = new(StringComparer.Ordinal);
    public SessionsViewModel(ISessionStore store, ICaptureTimeReader reader, IEnumerable<string> paths) { _store = store; _reader = reader; _paths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); }
    partial void OnSelectedGroupChanged(SessionGroup? value) => RefreshMembers();
    partial void OnSortChanged(string value) => RefreshMembers();
    private void RefreshMembers()
    {
        IEnumerable<CaptureMember> rows = SelectedGroup?.Members ?? [];
        rows = Sort == "文件名" ? rows.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            : Sort == "日期" ? rows.OrderBy(m => m.LocalTime?.Date).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            : rows.OrderBy(m => m.EffectiveTime ?? m.LocalTime).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
        Members = new ObservableCollection<CaptureMember>(rows);
        OnPropertyChanged(nameof(Members));
    }

    private void Show(IReadOnlyList<SessionGroup> groups)
    {
        _groups = groups;
        Groups = new ObservableCollection<SessionGroup>(groups);
        OnPropertyChanged(nameof(Groups));
        SelectedGroup = Groups.FirstOrDefault();
    }
    public async Task InitializeAsync() { IsBusy = true; try { _document = await _store.LoadAsync(); ThresholdMinutes = _document.Preferences.ThresholdMinutes; Sort = _document.Preferences.Sort; foreach (var p in _document.Preferences.CameraCorrections ?? []) _corrections[p.Key] = p.Value; Show(_document.Groups); Status = $"已加载 {Groups.Count} 个场次；可重新检测当前素材或撤销最近提交。"; } catch (Exception e) { Status = e.Message; } finally { IsBusy = false; } }
    [RelayCommand]
    private async Task DetectAsync()
    {
        if (IsBusy) return;
        if (!double.IsFinite(ThresholdMinutes) || ThresholdMinutes <= 0 || ThresholdMinutes > 525600)
        {
            Status = "阈值须在 0 到 525600 分钟之间。";
            return;
        }
        IsBusy = true;
        Progress = 0;
        Status = "正在读取 EXIF 拍摄时间…";
        _cancellation = new();
        var token = _cancellation.Token;
        var corrections = new Dictionary<string, double>(_corrections);
        var threshold = TimeSpan.FromMinutes(ThresholdMinutes);
        var savedGroups = _document.Groups.ToArray();
        var generation = ++_detectionGeneration;
        try
        {
            IProgress<double> reporter = new Progress<double>(value =>
            {
                if (generation == _detectionGeneration && IsBusy && !token.IsCancellationRequested)
                    Progress = value;
            });
            var result = await Task.Run(() =>
            {
                var members = new List<CaptureMember>(_paths.Length);
                var lastPercent = 0;
                for (var index = 0; index < _paths.Length; index++)
                {
                    token.ThrowIfCancellationRequested();
                    members.Add(_reader.Read(_paths[index]));
                    var percent = (int)((index + 1L) * 100 / _paths.Length);
                    if (percent > lastPercent)
                    {
                        reporter.Report(percent);
                        lastPercent = percent;
                    }
                }
                token.ThrowIfCancellationRequested();
                var ids = members.Select(member => member.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var preserved = savedGroups.Select(group => group with
                {
                    Members = group.Members.Where(member => !ids.Contains(member.Id)).ToArray()
                }).Where(group => group.Count > 0);
                var detected = SessionPlanner.Detect(members, threshold, corrections);
                var groups = preserved.Concat(detected.Groups).ToArray();
                token.ThrowIfCancellationRequested();
                if (lastPercent < 100) reporter.Report(100);
                return (Groups: groups, Cameras: members.Select(member => member.Camera).Distinct().ToArray(),
                    MemberCount: members.Count, ReviewCount: groups.Count(group => group.NeedsReview));
            }, token);
            token.ThrowIfCancellationRequested();
            Cameras.Clear();
            foreach (var cameraName in result.Cameras) Cameras.Add(cameraName);
            Camera = Cameras.FirstOrDefault() ?? "";
            Show(result.Groups);
            Progress = 100;
            Status = $"预览：{result.MemberCount} 个素材，{result.Groups.Length} 个场次，{result.ReviewCount} 组待确认。未保存。";
        }
        catch (OperationCanceledException) { Status = "已取消；已保存场次保持不变。"; }
        catch (Exception exception) { Status = exception.Message; }
        finally
        {
            IsBusy = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }
    [RelayCommand] private void Cancel() => _cancellation?.Cancel();
    [RelayCommand] private void CorrectCamera() { if (IsBusy) return; if (!double.IsFinite(CorrectionMinutes) || Math.Abs(CorrectionMinutes) > 525600) { Status = "修正分钟数无效。"; return; } _corrections[Camera] = CorrectionMinutes; Status = $"已设置 {Camera} 修正 {CorrectionMinutes:+0.##;-0.##;0} 分钟；重新检测后生效，提交后保存。"; }
    [RelayCommand] private void Split() { if (IsBusy || SelectedGroup is null || SelectedMember is null) return; var index = SelectedGroup.Members.ToList().FindIndex(m => m.Id == SelectedMember.Id); if (index <= 0) { Status = "请选择场次内第二个或之后的成员，在它之前增加边界。"; return; } Show(SessionPlanner.Split(_groups, Groups.IndexOf(SelectedGroup), index)); Status = "已增加边界；等待提交。"; }
    [RelayCommand] private void Merge() { if (IsBusy || SelectedGroup is null) return; var i = Groups.IndexOf(SelectedGroup); if (i >= Groups.Count - 1) { Status = "请选择最后一组之前的场次。"; return; } Show(SessionPlanner.Merge(_groups, i)); Status = "已移除边界并合并下一场；等待提交。"; }
    [RelayCommand] private async Task CommitAsync() { if (IsBusy) return; IsBusy = true; try { _document = await _store.CommitAsync(_document.Revision, _groups, new(ThresholdMinutes, Sort, new(_corrections))); Status = $"已保存 {Groups.Count} 个虚拟场次；原片未修改。"; } catch (Exception e) { Status = e.Message; } finally { IsBusy = false; } }
    [RelayCommand] private async Task UndoAsync() { if (IsBusy) return; IsBusy = true; try { _document = await _store.UndoAsync(_document.Revision); ThresholdMinutes = _document.Preferences.ThresholdMinutes; Sort = _document.Preferences.Sort; _corrections.Clear(); foreach (var pair in _document.Preferences.CameraCorrections ?? []) _corrections[pair.Key] = pair.Value; Show(_document.Groups); Status = "已撤销最近提交；原片未修改。"; } catch (Exception e) { Status = e.Message; } finally { IsBusy = false; } }
}
