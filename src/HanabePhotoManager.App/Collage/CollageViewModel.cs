using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using HanabePhotoManager.Core.Collage;
using HanabePhotoManager.App.Compression;
using HanabePhotoManager.Infrastructure.Sessions;
namespace HanabePhotoManager.App.Collage;
public sealed record CollagePreferences(int Width=3600,int Height=2400,int Layout=0,int Margin=48,int Gap=24,string Background="#FFFFFF",bool Crop=false,bool FaceHints=false,int Format=0);
public partial class CollageViewModel : ObservableObject
{
    public ObservableCollection<string> Photos { get; } = [];
    [ObservableProperty] private string status = "1 选图 → 2 自动排版 → 3 微调 → 4 导出。默认完整适配，不拉伸。";
    [ObservableProperty] private bool orderLocked;
    [ObservableProperty] private string? hero;
    [ObservableProperty] private int progress;
    [ObservableProperty] private string heroProvenance="未指定主图";
    public void SetManualHero(string? path){Hero=path;HeroProvenance=path is null?"未指定主图":"手动指定主图";}
    public async Task SuggestHeroAsync(CancellationToken token)
    {
        if(OrderLocked){Status="顺序已锁定，保留当前主图。";return;}
        if(Hero is not null){Status="已保留现有主图；如需重新建议，请先清除主图。";return;}
        var photos=await ImageCollageService.ProbeAsync(Photos.ToArray(),token);
        var suggested=await new CollageHeroSuggester().SuggestAsync(photos,token);
        Hero=suggested.PhotoId;HeroProvenance="自动建议主图："+Path.GetFileName(Hero)+"（评分 "+suggested.Score.ToString("F2")+"）";
        Status=HeroProvenance+"。"+suggested.Reason;
    }
    public int Generation { get; set; }
    public IReadOnlyList<CollagePlan> Candidates { get; set; } = [];
    public void Move(int from, int to) { if (!OrderLocked && from >= 0 && to >= 0 && from < Photos.Count && to < Photos.Count) Photos.Move(from, to); }
    public void Sort(bool time)
    {
        if(time) throw new InvalidOperationException("拍摄时间排序请使用可取消的异步方法。");
        if(OrderLocked)return;
        var sorted=Photos.OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).ToArray();
        Photos.Clear();foreach(var path in sorted)Photos.Add(path);Status="已按文件名排序。";
    }
    public void Swap(int first,int second)
    {
        if(OrderLocked||first<0||second<0||first>=Photos.Count||second>=Photos.Count)return;
        (Photos[first],Photos[second])=(Photos[second],Photos[first]);
    }
    public async Task SortByCaptureTimeAsync(CancellationToken token)
    {
        if(OrderLocked)return;
        var snapshot=Photos.ToArray();
        var members=await Task.Run(()=>
        {
            var reader=new ExifCaptureTimeReader();
            return snapshot.Select(path=>{token.ThrowIfCancellationRequested();return reader.Read(path);}).ToArray();
        },token);
        token.ThrowIfCancellationRequested();
        if(OrderLocked||!Photos.SequenceEqual(snapshot))return;
        bool mixed=members.Any(m=>m.UtcOffset.HasValue)&&members.Any(m=>m.LocalTime.HasValue&&!m.UtcOffset.HasValue);
        var sorted=members.OrderBy(m=>m.LocalTime.HasValue?m.UtcOffset.HasValue?0:1:2)
            .ThenBy(m=>m.LocalTime.HasValue&&m.UtcOffset.HasValue?new DateTimeOffset(m.LocalTime.Value,m.UtcOffset.Value).UtcDateTime:m.LocalTime)
            .ThenBy(m=>m.Path,StringComparer.OrdinalIgnoreCase).Select(m=>m.Path).ToArray();
        Photos.Clear();foreach(var path in sorted)Photos.Add(path);
        Status=$"已按 EXIF 拍摄时间排序；缺失 {members.Count(m=>!m.LocalTime.HasValue)} 张放末尾。"+(mixed?"有时区与无时区照片分组，未假定电脑时区。":"无时区按相机本地时间比较。");
    }    public async Task GenerateAsync(CollageSettings settings, bool faceHints, CancellationToken token)
    {
        var photos = await ImageCollageService.ProbeAsync(Photos.ToArray(),token);
        var plans = CollagePlanner.Candidates(photos,settings with { HeroId=Hero,Generation=Generation++ });
        string message = "已生成三套候选；基准边距分别增加 0 / 4 / 8 像素，换组继续调整边距，照片顺序不变。";
        if(faceHints) { var hints=await new CollageSubjectHints().ApplyAsync(plans,photos,token); plans=hints.Plans; message=hints.Message; }
        Candidates=plans; Status=message;
    }
    public async Task ExportAsync(int candidate, CollageExportOptions options, CancellationToken token)
    {
        var plan=Candidates[candidate]; Progress=0;
        var progress=new Progress<CollageProgress>(p=>{Progress=p.Processed*100/p.Total;Status=p.CurrentFile;});
        var result=await Task.Run(()=>new ImageCollageService().ComposeAsync(plan,options,progress,token),token);
        Status=result.Message+(result.OutputPath is null ? "" : " "+result.OutputPath);
    }
    private static string PreferencesPath => Path.Combine(HanabePhotoManager.App.Services.AppDataPaths.ProcessingRoot,"preferences","collage.json");
    public CollagePreferences LoadPreferences()
    {
        try { return File.Exists(PreferencesPath) ? JsonSerializer.Deserialize<CollagePreferences>(File.ReadAllText(PreferencesPath)) ?? new() : new(); }
        catch (Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) { Status="偏好读取失败，使用默认设置："+e.Message; return new(); }
    }
    public void SavePreferences(CollagePreferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
        string temporary=PreferencesPath+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {File.WriteAllText(temporary,JsonSerializer.Serialize(preferences));File.Move(temporary,PreferencesPath,true);}
        finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
}


