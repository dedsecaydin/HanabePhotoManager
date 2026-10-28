using System.IO;
using System.Reflection;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;
using Xunit;

namespace HanabePhotoManager.App.Tests.Imports;

[Collection(nameof(ProcessEnvironmentCollection))]
public sealed class ImportDuplicateExecutionTests : IDisposable
{
    private readonly string _root = Path.Combine(@"D:\HanabePhoto\.artifacts\duplicate-regression", Guid.NewGuid().ToString("N"));
    private readonly string? _previousDataRoot = Environment.GetEnvironmentVariable("HANABE_APP_DATA_DIR");
    private readonly LibraryDate _date = new(2026, 10, 18);

    public ImportDuplicateExecutionTests()
    {
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("HANABE_APP_DATA_DIR", Path.Combine(_root, "appdata"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SkipAll_WithRenamedExistingCopy_DoesNotTransferOrDeleteSource(bool move)
    {
        var (viewModel, group, target) = CreateFixture();
        var preview = new ImportPreviewItemViewModel(group, group.Category, viewModel.CategoryChoices, false, _date, null, "", 1);
        var method = typeof(MainWindowViewModel).GetMethod("RunImportAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)method.Invoke(viewModel, new object[]
        {
            new[] { preview }, move, new Dictionary<LibraryDate, string> { [_date] = target }
        })!;

        Assert.Contains("成功 0，跳过 1，失败 0", viewModel.ImportReport);
        Assert.Single(Directory.EnumerateFiles(target, "*.MP4", SearchOption.AllDirectories));
        Assert.True(File.Exists(group.Primary.FullPath));
        Assert.False(new ImportResumeStore().HasPending);
    }

    [Fact]
    public async Task Resume_WithOldUncheckedPlan_RechecksDuplicatesBeforeCopying()
    {
        var (viewModel, group, target) = CreateFixture();
        var destination = Path.Combine(target, "视频", "JK0002.MP4");
        var entry = new ImportResumeEntry
        {
            GroupKey = group.GroupKey, Category = group.Category.ToString(), PrimaryPath = group.Primary.FullPath,
            Year = _date.Year, Month = _date.Month, Day = _date.Day, TargetDateDirectory = target,
            Plan = new ImportPlanItem(Guid.NewGuid(), group,
                new[] { new PlannedFile(group.Primary, destination, destination + ".hanabe-part", ConflictKind.None) },
                ConflictKind.None, ImportItemState.Planned)
        };
        new ImportResumeStore().Save(new ImportResumeState { Entries = [entry] });

        await viewModel.ResumePendingImportAsync();

        Assert.Contains("成功 0，跳过 1，失败 0", viewModel.ImportReport);
        Assert.False(File.Exists(destination));
        Assert.True(File.Exists(group.Primary.FullPath));
    }

    [Theory]
    [InlineData(".HEIF")]
    [InlineData(".heic")]
    [InlineData(".HIF")]
    public async Task HeifFamily_ImportsIntoJpegFolderAndSkipsSecondImport(string extension)
    {
        var library = Path.Combine(_root, "library");
        var target = Path.Combine(library, _date.RelativePath);
        var viewModel = new MainWindowViewModel { LibraryRoot = library, CurrentPage = "Import", HanabeSoundEnabled = false };
        var path = Path.Combine(_root, $"PHOTO{extension}");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "example.heic"), path);
        var original = File.ReadAllBytes(path);
        var files = new[] { new SourceMediaFile(path, original.Length, DateTimeOffset.UtcNow) };
        var groups = new MediaGroupBuilder(new MediaClassifier(["ARW"])).Build(files);
        Assert.All(groups, group => Assert.Equal(MediaCategory.Jpeg, group.Category));
        var items = groups.Select((group, index) => new ImportPreviewItemViewModel(group, group.Category,
            viewModel.CategoryChoices, false, _date, null, "", index)).ToArray();
        var method = typeof(MainWindowViewModel).GetMethod("RunImportAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var arguments = new object[] { items, false, new Dictionary<LibraryDate, string> { [_date] = target } };

        await (Task)method.Invoke(viewModel, arguments)!;
        Assert.Contains("成功 1，跳过 0，失败 0", viewModel.ImportReport);
        var imported = Directory.GetFiles(Path.Combine(target, "JPG生图"));
        var jpeg = Assert.Single(imported, file => Path.GetExtension(file).Equals(".jpg", StringComparison.OrdinalIgnoreCase));
        using (var image = SixLabors.ImageSharp.Image.Load(jpeg))
        { Assert.True(image.Width > 0); Assert.True(image.Height > 0); }
        foreach (var source in files)
        {
            var destination = Assert.Single(imported, path =>
                Path.GetExtension(path).Equals(Path.GetExtension(source.FullPath), StringComparison.OrdinalIgnoreCase));
            Assert.Equal(File.ReadAllBytes(source.FullPath), File.ReadAllBytes(destination));
        }

        await (Task)method.Invoke(viewModel, arguments)!;
        Assert.Contains("成功 0，跳过 1，失败 0", viewModel.ImportReport);
        Assert.Equal(imported.Length, Directory.GetFiles(Path.Combine(target, "JPG生图")).Length);
        File.Delete(jpeg);
        await (Task)method.Invoke(viewModel, arguments)!;
        Assert.Contains("成功 0，跳过 1，失败 0", viewModel.ImportReport);
        Assert.True(File.Exists(jpeg));
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HeifMove_ConversionCanResumeAfterSourceDeletion(bool planContainsPrimary)
    {
        var library = Path.Combine(_root, "library");
        var target = Directory.CreateDirectory(Path.Combine(library, _date.RelativePath, "JPG生图")).FullName;
        var original = Path.Combine(target, "JK0001.HIF");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "example.heic"), original);
        var originalBytes = File.ReadAllBytes(original);
        var sourcePath = Path.Combine(_root, "already-moved.HIF");
        var source = new SourceMediaFile(sourcePath, originalBytes.Length, DateTimeOffset.UtcNow);
        var group = new MediaGroup("already-moved", MediaCategory.Jpeg, source, []);
        var file = new PlannedFile(source, original, original + ".hanabe-part", ConflictKind.None);
        var state = new ImportResumeState
        {
            DeleteSourcesAfterVerify = true,
            Entries = [new ImportResumeEntry
            {
                GroupKey = group.GroupKey, Category = "Jpeg", PrimaryPath = sourcePath,
                Year = _date.Year, Month = _date.Month, Day = _date.Day,
                TargetDateDirectory = Path.GetDirectoryName(target)!,
                Plan = new ImportPlanItem(Guid.NewGuid(), group, planContainsPrimary ? [file] : [], ConflictKind.None, ImportItemState.Planned),
                VerifiedFiles = [new VerifiedFileResult(file, await new Sha256FileHasher().ComputeSha256Async(original, default))]
            }]
        };
        new ImportResumeStore().Save(state);
        var viewModel = new MainWindowViewModel { LibraryRoot = library, CurrentPage = "Import", HanabeSoundEnabled = false };

        await viewModel.ResumePendingImportAsync();

        Assert.False(new ImportResumeStore().HasPending);
        Assert.False(File.Exists(sourcePath));
        Assert.Equal(originalBytes, File.ReadAllBytes(original));
        using var jpeg = SixLabors.ImageSharp.Image.Load(Path.ChangeExtension(original, ".JPG"));
        Assert.Equal(1280, jpeg.Width);
    }

    [Fact]
    public async Task LegacySkippedHeif_ResolvesExistingOriginalAndGeneratesMissingJpeg()
    {
        var library = Path.Combine(_root, "library");
        var dateDirectory = Path.Combine(library, _date.RelativePath);
        var category = Directory.CreateDirectory(Path.Combine(dateDirectory, "JPG生图")).FullName;
        var sourcePath = Path.Combine(_root, "PHOTO.HIF");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "example.heic"), sourcePath);
        var original = Path.Combine(category, "JK0001.HIF");
        File.Copy(sourcePath, original);
        var source = new SourceMediaFile(sourcePath, new FileInfo(sourcePath).Length, DateTimeOffset.UtcNow);
        var group = new MediaGroup("PHOTO", MediaCategory.Jpeg, source, []);
        var unused = Path.Combine(category, "JK0002.HIF");
        new ImportResumeStore().Save(new ImportResumeState
        {
            Entries = [new ImportResumeEntry
            {
                GroupKey = group.GroupKey, Category = "Jpeg", PrimaryPath = sourcePath, SkipTransfer = true,
                Year = _date.Year, Month = _date.Month, Day = _date.Day, TargetDateDirectory = dateDirectory,
                Plan = new ImportPlanItem(Guid.NewGuid(), group,
                    [new PlannedFile(source, unused, unused + ".hanabe-part", ConflictKind.None)], ConflictKind.None, ImportItemState.Planned)
            }]
        });
        var viewModel = new MainWindowViewModel { LibraryRoot = library, CurrentPage = "Import", HanabeSoundEnabled = false };
        await viewModel.ResumePendingImportAsync();
        Assert.True(File.Exists(Path.ChangeExtension(original, ".JPG")));
        Assert.False(File.Exists(unused));
        Assert.True(File.Exists(sourcePath));
        Assert.Contains("成功 0，跳过 1，失败 0", viewModel.ImportReport);
    }

    [Fact]
    public async Task KeepAll_OnlyCopiesAgainWhenExplicitlySelected()
    {
        var (viewModel, group, target) = CreateFixture();
        viewModel.KeepImportDuplicates = true;
        var preview = new ImportPreviewItemViewModel(group, group.Category, viewModel.CategoryChoices, false, _date, null, "", 1);
        var method = typeof(MainWindowViewModel).GetMethod("RunImportAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(viewModel, new object[]
        {
            new[] { preview }, false, new Dictionary<LibraryDate, string> { [_date] = target }
        })!;
        Assert.Contains("成功 1，跳过 0，失败 0", viewModel.ImportReport);
        Assert.Equal(2, Directory.EnumerateFiles(target, "*.MP4", SearchOption.AllDirectories).Count());
    }

    [Fact]
    public void DuplicateOptions_AreExclusiveAndDefaultToSkipping()
    {
        var viewModel = new MainWindowViewModel();
        Assert.True(viewModel.CheckDuplicatesOnImport);
        Assert.True(viewModel.SkipImportDuplicates);
        viewModel.KeepImportDuplicates = true;
        Assert.True(viewModel.KeepImportDuplicates);
        Assert.False(viewModel.SkipImportDuplicates);
        viewModel.ReviewImportDuplicates = true;
        Assert.True(viewModel.ReviewImportDuplicates);
        Assert.False(viewModel.KeepImportDuplicates);
        viewModel.SkipImportDuplicates = true;
        Assert.False(viewModel.ReviewImportDuplicates);
    }

    private (MainWindowViewModel, MediaGroup, string) CreateFixture()
    {
        var library = Path.Combine(_root, "library");
        var target = Path.Combine(library, _date.RelativePath);
        var category = Directory.CreateDirectory(Path.Combine(target, "视频"));
        var source = Path.Combine(_root, "C2417.MP4");
        File.WriteAllBytes(source, [1, 3, 5, 7, 9]);
        File.Copy(source, Path.Combine(category.FullName, "JK0001.MP4"));
        var group = new MediaGroup("C2417", MediaCategory.Video,
            new SourceMediaFile(source, 5, DateTimeOffset.UtcNow), []);
        var vm = new MainWindowViewModel { LibraryRoot = library, CurrentPage = "Import", HanabeSoundEnabled = false };
        return (vm, group, target);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HANABE_APP_DATA_DIR", _previousDataRoot);
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
