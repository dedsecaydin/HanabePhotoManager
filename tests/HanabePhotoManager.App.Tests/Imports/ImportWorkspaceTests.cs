using System.IO;
using System.Reflection;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Sessions;
using Xunit;

namespace HanabePhotoManager.App.Tests.Imports;

[Collection(nameof(ProcessEnvironmentCollection))]
public sealed class ImportWorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(@"D:\HanabePhoto\.artifacts\multi-source-tests", Guid.NewGuid().ToString("N"));
    private readonly string? _previous = Environment.GetEnvironmentVariable("HANABE_APP_DATA_DIR");
    public ImportWorkspaceTests() { Directory.CreateDirectory(_root); Environment.SetEnvironmentVariable("HANABE_APP_DATA_DIR", Path.Combine(_root, "data")); }
    private string Source(string device, byte value)
    {
        var dir = Path.Combine(_root, device, "1027"); Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "C0001.MP4"), [value, 2, 3]);
        File.WriteAllText(Path.Combine(dir, "C0001M01.XML"), device);
        return dir;
    }
    private MainWindowViewModel CreateVm()
    {
        var library = Path.Combine(_root, "library"); Directory.CreateDirectory(library);
        return new MainWindowViewModel { LibraryRoot = library };
    }
    [Fact] public async Task MultipleSources_KeepSameNamesAndSidecarsSeparate_AndDeduplicateOverlaps()
    {
        var a = Source("card-a", 1); var b = Source("card-b", 2); var vm = CreateVm();
        await vm.PrepareMultipleImportSourcesAsync([Path.GetDirectoryName(a)!, a, b, b.ToUpperInvariant()]);
        Assert.Equal(2, vm.ImportSourceCards.Count);
        Assert.Equal(2, vm.ImportItems.Count);
        Assert.All(vm.ImportItems, item => Assert.All(item.Group.Sidecars, file => Assert.Equal(Path.GetDirectoryName(item.PrimaryPath), Path.GetDirectoryName(file.FullPath))));
        Assert.False(vm.IsImportDateFolderPreflightVisible);
        Assert.Empty(Directory.EnumerateFiles(vm.LibraryRoot, "*", SearchOption.AllDirectories));
    }
    [Fact] public async Task ClassificationAndSessionDrag_AreUndoableAndNeverMutateSources()
    {
        var a = Source("card-a", 1); var b = Source("card-b", 2); var vm = CreateVm();
        await vm.PrepareMultipleImportSourcesAsync([a,b]);
        var items = vm.ImportItems.ToArray(); var original = items.Select(i => File.ReadAllBytes(i.PrimaryPath)).ToArray();
        var category = vm.ImportDropCategories.First(c => c.Category != MediaCategory.Video);
        vm.NewImportSessionName = "上午棚拍"; vm.AddImportSessionCommand.Execute(null);
        vm.AssignImportOrganization(items, category, "上午棚拍");
        Assert.All(items, i => { Assert.Equal(category, i.SelectedCategory); Assert.Equal("上午棚拍", i.ImportSessionName); });
        vm.UndoImportOrganizationCommand.Execute(null);
        Assert.All(items, i => { Assert.Equal(MediaCategory.Video, i.SelectedCategory.Category); Assert.Empty(i.ImportSessionName); });
        for (var i = 0; i < items.Length; i++) Assert.Equal(original[i], File.ReadAllBytes(items[i].PrimaryPath));
        vm.IsBusy = true; vm.AssignImportOrganization(items, category, "上午棚拍");
        Assert.All(items, i => Assert.Equal(MediaCategory.Video, i.SelectedCategory.Category));
    }
    [Fact] public async Task SourceAndSearchFilters_KeepExcludedFilesVisibleForReselection()
    {
        var a=Source("card-a",1); var b=Source("card-b",2); var vm=CreateVm();
        await vm.PrepareMultipleImportSourcesAsync([a,b]);
        vm.ImportItems[0].IsSelected=false;
        Assert.Equal(2,vm.ImportSections.Sum(s=>s.Items.Count));
        vm.FilterImportSourceCommand.Execute(a);
        Assert.Single(vm.ImportSections.SelectMany(s=>s.Items));
        vm.ImportSearchText="does-not-exist"; Assert.Empty(vm.ImportSections);
        vm.ImportSearchText=""; vm.FilterImportSourceCommand.Execute(null);
        Assert.Equal(2,vm.ImportSections.Sum(s=>s.Items.Count));
    }
    [Fact] public async Task DisconnectedSource_DoesNotHideOtherDeviceResults()
    {
        var vm=CreateVm(); await vm.PrepareMultipleImportSourcesAsync([Source("card-a",1),Path.Combine(_root,"missing")]);
        Assert.Single(vm.ImportItems); Assert.Equal(2,vm.ImportSourceCards.Count);
    }
    [Fact] public async Task SessionWriter_IsIdempotent_AndSeparatesSameNamedBatches()
    {
        var a=Path.Combine(Source("card-a",1),"C0001.MP4"); var b=Path.Combine(Source("card-b",2),"C0001.MP4");
        var store=new JsonSessionStore(Path.Combine(_root,"sessions.json")); var reader=new ExifCaptureTimeReader();
        await ImportSessionWriter.AssignAsync(store,reader,"场次 1",a,default,"batch-a");
        var first=await store.LoadAsync();
        await ImportSessionWriter.AssignAsync(store,reader,"场次 1",a,default,"batch-a");
        Assert.Equal(first.Revision,(await store.LoadAsync()).Revision);
        await ImportSessionWriter.AssignAsync(store,reader,"场次 1",b,default,"batch-b");
        Assert.Equal(2,(await store.LoadAsync()).Groups.Count);
    }
    [Fact] public async Task ImportedSession_UsesDestination_AndResumeSnapshotRetainsAssignment()
    {
        var vm=CreateVm(); vm.CurrentPage = "Import"; var source=Source("card-a",1);
        await vm.PrepareMultipleImportSourcesAsync([source]);
        var item=Assert.Single(vm.ImportItems); item.ImportSessionName="跨设备场次";
        var date=item.TargetDate!.Value; var target=Path.Combine(vm.LibraryRoot,"10月","10.27"); Directory.CreateDirectory(target);
        var build=typeof(MainWindowViewModel).GetMethod("BuildResumeEntry",BindingFlags.Static|BindingFlags.NonPublic)!;
        var entry=(ImportResumeEntry)build.Invoke(null,[item,date,target])!;
        var resume=new ImportResumeStore(Path.Combine(_root,"roundtrip.json")); resume.Save(new(){Entries=[entry]});
        Assert.Equal("跨设备场次",Assert.Single(resume.Load()!.Entries).SessionName);
        var run=typeof(MainWindowViewModel).GetMethod("RunImportAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var transfer = (Task)run.Invoke(vm,[new[]{item},false,new Dictionary<LibraryDate,string>{{date,target}}])!;
        var completed = await Task.WhenAny(transfer, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.True(completed == transfer, $"Import stalled: {vm.ProgressLabel} | {vm.ImportReport}");
        await transfer;
        var saved=await new JsonSessionStore(ImportSessionWriter.StorePath(vm.LibraryRoot)).LoadAsync();
        var group=Assert.Single(saved.Groups); Assert.Equal("跨设备场次",group.Name);
        Assert.All(group.Members,m=>{ Assert.StartsWith(vm.LibraryRoot,m.Path); Assert.True(File.Exists(m.Path)); });
        Assert.True(File.Exists(item.PrimaryPath));
    }
    [Fact] public void InvalidThumbnail_DoesNotKeepFileLocked()
    {
        var path=Path.Combine(Source("invalid-thumbnail",1),"C0001.MP4");
        var decode=typeof(MainWindowViewModel).GetMethod("TryLoadWpfThumbnail",BindingFlags.Static|BindingFlags.NonPublic)!;
        Assert.Null(decode.Invoke(null,[path,260]));
        using var exclusive=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        Assert.Equal(3,exclusive.Length);
    }
    public void Dispose() { Environment.SetEnvironmentVariable("HANABE_APP_DATA_DIR",_previous); }
}
