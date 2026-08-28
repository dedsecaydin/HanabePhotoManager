using FluentAssertions;
using HanabePhotoManager.App.DateFolders;
using HanabePhotoManager.App.Services;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class DateFolderManagementViewModelTests
{
    [Fact]
    public async Task SaveAllAsync_ContinuesAfterOneRowFailsAndKeepsFailedEdit()
    {
        var service = new RecordingDateFolderService(
            results:
            [
                Result(DateFolderRenameStatus.Success, @"D:\\Library\\08月\\08.01_旅行"),
                Result(DateFolderRenameStatus.TargetExists, @"D:\\Library\\08月\\08.02_婚礼")
            ]);
        var viewModel = CreateViewModel(service, DirtyEntries());

        await viewModel.SaveAllAsync();

        viewModel.Summary.Should().Be("保存完成：成功 1，跳过 0，失败 1");
        viewModel.Items[0].IsDirty.Should().BeFalse();
        viewModel.Items[0].FullPath.Should().Be(@"D:\\Library\\08月\\08.01_旅行");
        viewModel.Items[1].IsDirty.Should().BeTrue();
        viewModel.Items[1].StatusText.Should().Contain("目标文件夹已存在");
        service.RenameCalls.Should().HaveCount(2);

        viewModel.Items[1].EditedRemark = "新婚礼";
        viewModel.Items[1].StatusText.Should().Be("待保存。");
    }

    [Fact]
    public async Task SaveAllAsync_SkipsCleanRowsWithoutCallingRename()
    {
        var service = new RecordingDateFolderService(
            results: [Result(DateFolderRenameStatus.Success, @"D:\\Library\\08月\\08.01_旅行")]);
        var entries = DirtyEntries();
        entries[1] = new DateFolderEntry(8, 2, "婚礼", @"D:\\Library\\08月\\08.02_婚礼");
        var viewModel = CreateViewModel(service, entries);

        await viewModel.SaveAllAsync();

        viewModel.Summary.Should().Be("保存完成：成功 1，跳过 1，失败 0");
        service.RenameCalls.Should().ContainSingle();
        viewModel.Items[1].StatusText.Should().Contain("无需保存");
    }

    [Fact]
    public async Task SaveAllAsync_TreatsNoChangeAsSkippedAndClearsDirtyState()
    {
        var service = new RecordingDateFolderService(
            results: [Result(DateFolderRenameStatus.NoChange, @"D:\\Library\\08月\\08.01")]);
        var viewModel = CreateViewModel(service,
        [
            new DateFolderEntry(8, 1, "", @"D:\\Library\\08月\\08.01")
        ]);
        viewModel.Items[0].EditedRemark = "  ";

        await viewModel.SaveAllAsync();

        viewModel.Summary.Should().Be("保存完成：成功 0，跳过 1，失败 0");
        viewModel.Items[0].IsDirty.Should().BeFalse();
        viewModel.Items[0].StatusText.Should().Contain("无需保存");
    }

    [Fact]
    public async Task SaveAllAsync_UsesTheServiceNormalizedRemarkAsTheNewBaseline()
    {
        var service = new RecordingDateFolderService(
            results: [Result(DateFolderRenameStatus.Success, @"D:\\Library\\08月\\08.01_旅行", "旅行")]);
        var viewModel = CreateViewModel(service,
        [
            new DateFolderEntry(8, 1, "", @"D:\\Library\\08月\\08.01")
        ]);
        viewModel.Items[0].EditedRemark = " _ 旅行 - ";

        await viewModel.SaveAllAsync();

        viewModel.Items[0].OriginalRemark.Should().Be("旅行");
        viewModel.Items[0].EditedRemark.Should().Be("旅行");
        viewModel.Items[0].IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAllAsync_ContinuesAfterServiceThrowsAndKeepsThatEdit()
    {
        var service = new RecordingDateFolderService(
            results: [Result(DateFolderRenameStatus.Success, @"D:\\Library\\08月\\08.02_婚礼")],
            exceptionOnCall: 0,
            exception: new InvalidOperationException("simulated failure"));
        var viewModel = CreateViewModel(service, DirtyEntries());

        await viewModel.SaveAllAsync();

        viewModel.Summary.Should().Be("保存完成：成功 1，跳过 0，失败 1");
        viewModel.Items[0].IsDirty.Should().BeTrue();
        viewModel.Items[0].StatusText.Should().Contain("保存失败");
        viewModel.Items[1].IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAsync_WithEmptyLibraryRoot_DoesNotCallServiceAndExplainsWhatToDo()
    {
        var service = new RecordingDateFolderService();
        var viewModel = new DateFolderManagementViewModel(service);

        await viewModel.RefreshAsync();

        service.ScanCalls.Should().Be(0);
        viewModel.Items.Should().BeEmpty();
        viewModel.Summary.Should().Be("请先选择照片库根目录。");
    }

    [Fact]
    public async Task RefreshAsync_ReplacesRowsWithScannedEntries()
    {
        var service = new RecordingDateFolderService(
            scanEntries:
            [
                new DateFolderEntry(8, 1, "旅行", @"D:\\Library\\08月\\08.01_旅行"),
                new DateFolderEntry(8, 2, "婚礼", @"D:\\Library\\08月\\08.02_婚礼")
            ]);
        var viewModel = new DateFolderManagementViewModel(service)
        {
            LibraryRoot = @"D:\\Library"
        };

        await viewModel.RefreshAsync();

        service.ScanCalls.Should().Be(1);
        service.ScannedRoots.Should().ContainSingle().Which.Should().Be(@"D:\\Library");
        viewModel.Items.Select(item => item.EditedRemark).Should().Equal("旅行", "婚礼");
        viewModel.Summary.Should().Be("已加载 2 个日期文件夹。");
    }

    [Fact]
    public async Task RefreshAsync_WhenServiceThrowsAnyException_ReportsAReadableSummary()
    {
        var viewModel = new DateFolderManagementViewModel(
            new RecordingDateFolderService(scanException: new InvalidOperationException("模拟扫描失败")))
        {
            LibraryRoot = @"D:\\Library"
        };

        await viewModel.RefreshAsync();

        viewModel.Items.Should().BeEmpty();
        viewModel.Summary.Should().Be("刷新失败：模拟扫描失败");
    }

    private static DateFolderManagementViewModel CreateViewModel(
        RecordingDateFolderService service,
        IReadOnlyList<DateFolderEntry> entries)
    {
        var viewModel = new DateFolderManagementViewModel(service)
        {
            LibraryRoot = @"D:\\Library"
        };
        foreach (var entry in entries)
        {
            var item = new DateFolderItemViewModel(entry)
            {
                EditedRemark = entry.Day == 1 ? "旅行" : "婚礼"
            };
            viewModel.Items.Add(item);
        }

        return viewModel;
    }

    private static List<DateFolderEntry> DirtyEntries() =>
    [
        new DateFolderEntry(8, 1, "旧旅行", @"D:\\Library\\08月\\08.01_旧旅行"),
        new DateFolderEntry(8, 2, "旧婚礼", @"D:\\Library\\08月\\08.02_旧婚礼")
    ];

    private static DateFolderRenameResult Result(
        DateFolderRenameStatus status,
        string effectivePath,
        string effectiveRemark = "") =>
        new(status, effectivePath, effectivePath, EffectiveRemark: effectiveRemark);

    private sealed class RecordingDateFolderService : IDateFolderService
    {
        private readonly Queue<DateFolderRenameResult> _results;
        private readonly int? _exceptionOnCall;
        private readonly Exception _exception;
        private readonly Exception? _scanException;

        public RecordingDateFolderService(
            IReadOnlyList<DateFolderEntry>? scanEntries = null,
            IReadOnlyList<DateFolderRenameResult>? results = null,
            int? exceptionOnCall = null,
            Exception? exception = null,
            Exception? scanException = null)
        {
            ScanEntries = scanEntries ?? [];
            _results = new Queue<DateFolderRenameResult>(results ?? []);
            _exceptionOnCall = exceptionOnCall;
            _exception = exception ?? new IOException("simulated failure");
            _scanException = scanException;
        }

        public int ScanCalls { get; private set; }
        public List<string> ScannedRoots { get; } = [];
        public List<(string SourcePath, string Remark)> RenameCalls { get; } = [];
        public IReadOnlyList<DateFolderEntry> ScanEntries { get; }

        public DateFolderScanResult Scan(string libraryRoot)
        {
            ScanCalls++;
            ScannedRoots.Add(libraryRoot);
            if (_scanException is not null)
            {
                throw _scanException;
            }

            return DateFolderScanResult.Success(ScanEntries);
        }

        public DateFolderRenameResult RenameRemark(string sourcePath, string remark)
        {
            var callIndex = RenameCalls.Count;
            RenameCalls.Add((sourcePath, remark));
            if (callIndex == _exceptionOnCall)
            {
                throw _exception;
            }

            return _results.Dequeue();
        }
    }
}
