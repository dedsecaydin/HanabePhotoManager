using System.Globalization;
using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Cloud;
using HanabePhotoManager.Core.Cloud;
using HanabePhotoManager.Infrastructure.Cloud;
using Xunit;

namespace HanabePhotoManager.App.Tests.Cloud;

/// <summary>
/// Verifies the cloud page's right-hand overview is driven by the real
/// CloudHubViewModel wiring (account state, capacity ring, transfer queue)
/// and that no hardcoded placeholder data remains.
/// </summary>
public sealed class CloudOverviewWiringTests
{
    [Fact]
    public async Task AccountOverview_ReflectsAuthenticatedAccountState()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();

        context.ViewModel.AccountState.IsAuthenticated.Should().BeTrue();
        context.ViewModel.AccountTitle.Should().Be("模拟网盘");
        context.ViewModel.AccountBadgeText.Should().Be("已连接");
        context.ViewModel.IsAccountConnected.Should().BeTrue();
        context.ViewModel.HasCapacityInfo.Should().BeTrue();
        context.ViewModel.UsedPercent.Should().BeApproximately(4d / 1024d, 0.0001);
        context.ViewModel.UsedPercentText.Should().Be("0%");
        context.ViewModel.UsedBytesText.Should().Be("4 B");
        // FormatBytes 实际规则：1MB 以下直接显示 B（无 KB 档），1024 B 显示 "1024 B"。
        context.ViewModel.CapacityText.Should().Be("总容量 1024 B · 剩余 1020 B");
        context.ViewModel.AccountSubtitle.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AccountOverview_Unauthenticated_ShowsEmptyStateNotFakeNumbers()
    {
        using var context = CloudViewModelTestData.Create();
        context.Provider.AccountState = new CloudAccountState(
            CloudProviderKind.Baidu,
            false,
            "百度网盘",
            0,
            0,
            "未登录 · 未找到已保存的 API 会话");
        await context.ViewModel.InitializeAsync();

        context.ViewModel.AccountTitle.Should().Be("百度网盘");
        context.ViewModel.IsAccountConnected.Should().BeFalse();
        context.ViewModel.AccountBadgeText.Should().Be("未接入");
        context.ViewModel.AccountSubtitle.Should().Be("未登录 · 未找到已保存的 API 会话");
        context.ViewModel.UsedPercent.Should().Be(0);
        context.ViewModel.UsedPercentText.Should().Be("—");
        context.ViewModel.UsedBytesText.Should().Be("—");
        context.ViewModel.HasCapacityInfo.Should().BeFalse();
        context.ViewModel.CapacityText.Should().Be("暂无容量信息");
        context.ViewModel.TransferJobs.Should().BeEmpty();
        context.ViewModel.HasTransferJobs.Should().BeFalse();
    }

    [Fact]
    public async Task TransferQueue_LoadsPersistedJobsIntoInspector()
    {
        var root = Path.Combine(Path.GetTempPath(), "HanabePhotoManager.Tests", $"cloud-queue-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var queuePath = Path.Combine(root, "transfers.json");
            var store = new JsonCloudTransferQueueStore(queuePath);
            var localFile = Path.Combine(Path.GetTempPath(), "IMG_4821.jpg");
            var running = new CloudTransferJob(
                Guid.NewGuid(),
                CloudProviderKind.Simulated,
                new CloudPath("/相册/2026-08"),
                CloudTransferPriority.Required,
                CloudTransferState.Running,
                [new CloudTransferFile(localFile, new CloudRelativePath("相册/2026-08/IMG_4821.jpg"), 5_900_000, null, 3_600_000)],
                DateTimeOffset.UtcNow);
            var pending = new CloudTransferJob(
                Guid.NewGuid(),
                CloudProviderKind.Simulated,
                new CloudPath("/视频"),
                CloudTransferPriority.Opportunistic,
                CloudTransferState.Pending,
                [new CloudTransferFile(Path.Combine(Path.GetTempPath(), "Vlog.mp4"), new CloudRelativePath("视频/Vlog.mp4"), 100_000_000, null)],
                DateTimeOffset.UtcNow);
            await store.SaveAsync([running, pending]);

            var provider = new StubCloudProvider(new Dictionary<string, IReadOnlyList<CloudObject>> { ["/"] = [] });
            var index = new MemoryIndex();
            using var cache = new MemoryCache();
            using var viewModel = new CloudHubViewModel(
                provider,
                index,
                cache,
                new TrackingSynchronizationContext(),
                store);
            await viewModel.InitializeAsync();

            viewModel.HasTransferJobs.Should().BeTrue();
            viewModel.TransferJobs.Should().HaveCount(2);
            viewModel.ActiveTransferCount.Should().Be(2);
            var first = viewModel.TransferJobs[0];
            first.Title.Should().Be("IMG_4821.jpg");
            first.StateText.Should().Be("传输中");
            first.Progress.Should().BeApproximately(3_600_000d / 5_900_000d, 0.0001);
            first.Subtitle.Should().Contain("/相册/2026-08");
            viewModel.TransferJobs[1].StateText.Should().Be("排队中");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task CloudPageFactory_BaiduWithoutSession_ReportsUnauthenticatedStateAndLoadsQueue()
    {
        var root = Path.Combine(Path.GetTempPath(), "HanabePhotoManager.Tests", $"cloud-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var queueStore = new JsonCloudTransferQueueStore(Path.Combine(root, "transfers.json"));
            await queueStore.SaveAsync(
            [
                new CloudTransferJob(
                    Guid.NewGuid(),
                    CloudProviderKind.Baidu,
                    new CloudPath("/相册"),
                    CloudTransferPriority.Required,
                    CloudTransferState.Pending,
                    [new CloudTransferFile(Path.Combine(Path.GetTempPath(), "a.jpg"), new CloudRelativePath("相册/a.jpg"), 1024, null)],
                    DateTimeOffset.UtcNow)
            ]);

            using var viewModel = await CloudPage.CreateCloudHubViewModelAsync(
                root,
                isQuark: false,
                new TrackingSynchronizationContext());

            viewModel.AccountState.Provider.Should().Be(CloudProviderKind.Baidu);
            viewModel.AccountState.IsAuthenticated.Should().BeFalse();
            viewModel.AccountState.UsedBytes.Should().Be(0);
            viewModel.AccountState.TotalBytes.Should().Be(0);

            await viewModel.InitializeAsync();

            // 未登录：标题仍是真实网盘名，未接入原因由副标题如实说明，不伪造登录/容量。
            viewModel.AccountTitle.Should().Be("百度网盘");
            viewModel.AccountSubtitle.Should().Be("未登录 · 未找到已保存的 API 会话");
            viewModel.AccountBadgeText.Should().Be("未接入");
            viewModel.IsAccountConnected.Should().BeFalse();
            viewModel.UsedPercentText.Should().Be("—");
            viewModel.CapacityText.Should().Be("暂无容量信息");

            viewModel.HasTransferJobs.Should().BeTrue();
            viewModel.TransferJobs.Should().HaveCount(1);
            viewModel.ActiveTransferCount.Should().Be(1);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task CloudPageFactory_Quark_ReportsNotIntegratedState()
    {
        var root = Path.Combine(Path.GetTempPath(), "HanabePhotoManager.Tests", $"cloud-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using var viewModel = await CloudPage.CreateCloudHubViewModelAsync(
                root,
                isQuark: true,
                new TrackingSynchronizationContext());

            viewModel.AccountState.Provider.Should().Be(CloudProviderKind.Quark);
            viewModel.AccountState.IsAuthenticated.Should().BeFalse();
            viewModel.AccountState.UsedBytes.Should().Be(0);
            viewModel.AccountState.TotalBytes.Should().Be(0);

            await viewModel.InitializeAsync();

            // 夸克连接器尚未实现：标题仍是真实网盘名，未接入原因由副标题如实说明，不伪造数据。
            viewModel.AccountTitle.Should().Be("夸克网盘");
            viewModel.AccountSubtitle.Should().Be("未接入 · 夸克网盘连接器尚未实现");
            viewModel.AccountBadgeText.Should().Be("未接入");
            viewModel.IsAccountConnected.Should().BeFalse();
            viewModel.UsedPercentText.Should().Be("—");
            viewModel.CapacityText.Should().Be("暂无容量信息");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CloudPage_NoLongerContainsHardcodedPlaceholderData()
    {
        var xaml = File.ReadAllText(SourcePath("src", "HanabePhotoManager.App", "Cloud", "CloudPage.xaml"));
        var code = File.ReadAllText(SourcePath("src", "HanabePhotoManager.App", "Cloud", "CloudPage.xaml.cs"));

        xaml.Should().NotContain("超级会员");
        xaml.Should().NotContain("hanabe@outlook.com");
        xaml.Should().NotContain("214.6");
        xaml.Should().NotContain("1,834");
        xaml.Should().NotContain("12,408");
        xaml.Should().NotContain("IMG_4821.jpg");
        xaml.Should().NotContain("旅行Vlog");
        xaml.Should().NotContain("暂停全部");

        xaml.Should().Contain("{Binding AccountTitle}");
        xaml.Should().Contain("{Binding AccountSubtitle}");
        xaml.Should().Contain("{Binding AccountBadgeText}");
        xaml.Should().Contain("{Binding UsedPercent,");
        xaml.Should().Contain("{Binding UsedPercentText}");
        xaml.Should().Contain("{Binding UsedBytesText}");
        xaml.Should().Contain("{Binding CapacityText}");
        xaml.Should().Contain("{Binding ActiveTransferCount}");
        xaml.Should().Contain("{Binding TransferJobs}");
        xaml.Should().Contain("{Binding HasTransferJobs}");

        code.Should().Contain("CreateCloudHubViewModelAsync");
        code.Should().Contain("UnauthenticatedCloudProvider");
        code.Should().Contain("_viewModel.RefreshAsync()");
        code.Should().Contain("JsonCloudTransferQueueStore");
        code.Should().Contain("EncryptedCloudSessionStore");
    }

    [Fact]
    public void PercentToArc_ProducesEmptyOrStrokedGeometry()
    {
        var converter = new PercentToArcGeometryConverter();
        Convert(0).Should().Be(System.Windows.Media.Geometry.Empty);
        Convert(-1).Should().Be(System.Windows.Media.Geometry.Empty);
        var half = Convert(0.68);
        half.Should().NotBe(System.Windows.Media.Geometry.Empty);
        half.Bounds.Width.Should().BeGreaterThan(0);
        Convert(1).Should().NotBe(System.Windows.Media.Geometry.Empty);

        System.Windows.Media.Geometry Convert(double value) =>
            (System.Windows.Media.Geometry)converter.Convert(
                value, typeof(System.Windows.Media.Geometry), null, CultureInfo.InvariantCulture)!;
    }

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;
        directory.Should().NotBeNull();
        return Path.Combine([directory!.FullName, .. parts]);
    }
}
