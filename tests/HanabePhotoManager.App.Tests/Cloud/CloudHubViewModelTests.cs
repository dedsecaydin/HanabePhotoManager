using System.Collections.Concurrent;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using HanabePhotoManager.App.Cloud;
using HanabePhotoManager.Core.Cloud;
using Xunit;

namespace HanabePhotoManager.App.Tests.Cloud;

public sealed class CloudHubViewModelTests
{
    [Fact]
    public async Task Initialize_LoadsAccountAndRootItems()
    {
        using var context = CloudViewModelTestData.Create();

        await context.ViewModel.InitializeAsync();

        context.ViewModel.AccountState.IsAuthenticated.Should().BeTrue();
        context.ViewModel.CurrentPath.Value.Should().Be("/");
        context.ViewModel.Items.Should().HaveCount(2);
        context.ViewModel.IsBusy.Should().BeFalse();
        context.ViewModel.ProgressValue.Should().Be(1);
    }

    [Fact]
    public async Task Initialize_IndexesEveryListedRootItem()
    {
        using var context = CloudViewModelTestData.Create();

        await context.ViewModel.InitializeAsync();

        context.Index.UpsertBatches.Should().ContainSingle();
        context.Index.UpsertBatches.Single().Select(item => item.Name)
            .Should().BeEquivalentTo("photos", "readme.jpg");
    }

    [Fact]
    public async Task Initialize_VisuallyAppendsEachScannedItemBeforeListingCompletes()
    {
        using var context = CloudViewModelTestData.Create();
        var secondItemReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecondItem = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Provider.BeforeYieldAsync = async (_, _, index, token) =>
        {
            if (index == 1)
            {
                secondItemReached.TrySetResult();
                await releaseSecondItem.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            }
        };

        var initializing = context.ViewModel.InitializeAsync();
        await secondItemReached.Task.WaitAsync(TimeSpan.FromSeconds(5));

        context.ViewModel.Items.Should().ContainSingle();
        context.ViewModel.ScannedItemCount.Should().Be(1);
        context.ViewModel.IsProgressIndeterminate.Should().BeTrue();
        context.ViewModel.ProgressText.Should().Contain("1");
        releaseSecondItem.TrySetResult();
        await initializing.WaitAsync(TimeSpan.FromSeconds(5));
        context.ViewModel.ScannedItemCount.Should().Be(2);
        context.ViewModel.IsProgressIndeterminate.Should().BeFalse();
    }

    [Fact]
    public async Task Initialize_PublishesAuthenticatedAccountBeforeSlowListingCompletes()
    {
        using var context = CloudViewModelTestData.Create();
        var listingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseListing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Provider.BeforeListAsync = async (_, token) =>
        {
            listingStarted.TrySetResult();
            await releaseListing.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        };

        var initializing = context.ViewModel.InitializeAsync();
        await listingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        context.ViewModel.AccountState.IsAuthenticated.Should().BeTrue();
        context.ViewModel.StatusText.Should().Contain("扫描");
        releaseListing.TrySetResult();
        await initializing.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Initialize_UnauthenticatedAccountShowsLoginPromptWithoutListingOrIndexing()
    {
        using var context = CloudViewModelTestData.Create();
        context.Provider.AccountState = new CloudAccountState(
            CloudProviderKind.Simulated, false, "模拟网盘", 0, 1024, "未登录");

        await context.ViewModel.InitializeAsync();

        context.ViewModel.AccountState.IsAuthenticated.Should().BeFalse();
        context.ViewModel.StatusText.Should().Contain("登录");
        context.ViewModel.Items.Should().BeEmpty();
        context.Provider.ListedPaths.Should().BeEmpty();
        context.Index.UpsertBatches.Should().BeEmpty();
    }

    [Fact]
    public async Task Initialize_AfterSuccessfulScanThenUnauthenticated_ClearsPreviousScanState()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();
        var file = context.ViewModel.Items.Single(item => !item.IsFolder);
        await context.ViewModel.OpenItemAsync(file);
        context.ViewModel.Items.Should().NotBeEmpty();
        context.ViewModel.SelectedPreviewPath.Should().NotBeNull();

        context.Provider.AccountState = new CloudAccountState(
            CloudProviderKind.Simulated, false, "模拟网盘", 0, 1024, "未登录");

        await context.ViewModel.InitializeAsync();

        context.ViewModel.AccountState.IsAuthenticated.Should().BeFalse();
        context.ViewModel.CurrentPath.Value.Should().Be("/");
        context.ViewModel.Items.Should().BeEmpty();
        context.ViewModel.SelectedPreviewPath.Should().BeNull();
        context.ViewModel.ProgressValue.Should().Be(0);
        context.ViewModel.ScannedItemCount.Should().Be(0);
        context.ViewModel.ProgressText.Should().Contain("登录");
        context.ViewModel.IsProgressIndeterminate.Should().BeFalse();
        context.ViewModel.StatusText.Should().Contain("登录");
    }

    [Fact]
    public async Task Initialize_ListFailureKeepsPublishedAuthenticatedAccount()
    {
        using var context = CloudViewModelTestData.Create();
        context.Provider.ListException = new IOException("listing failed");

        await context.ViewModel.InitializeAsync();

        context.ViewModel.AccountState.IsAuthenticated.Should().BeTrue();
        context.ViewModel.StatusText.Should().Contain("listing failed");
    }

    [Fact]
    public async Task Initialize_IndexFailureKeepsPublishedAuthenticatedAccount()
    {
        using var context = CloudViewModelTestData.Create();
        context.Index.UpsertException = new IOException("index failed");

        await context.ViewModel.InitializeAsync();

        context.ViewModel.AccountState.IsAuthenticated.Should().BeTrue();
        context.ViewModel.HasError.Should().BeTrue();
        context.ViewModel.ErrorMessage.Should().Contain("index failed");
        context.ViewModel.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task OpenFolder_ReplacesItemsWithDirectChildren()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.OpenFolderAsync(
            context.ViewModel.Items.Single(item => item.Kind == CloudObjectKind.Folder));

        context.ViewModel.CurrentPath.Value.Should().Be("/photos");
        context.ViewModel.Items.Should().ContainSingle(item => item.Name == "a.jpg");
        context.Index.UpsertBatches.Should().HaveCount(2);
    }

    [Fact]
    public async Task Back_NavigatesToParentAndIsDisabledAtRoot()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();
        await context.ViewModel.OpenFolderAsync(
            context.ViewModel.Items.Single(item => item.Kind == CloudObjectKind.Folder));
        context.ViewModel.BackCommand.CanExecute(null).Should().BeTrue();

        await context.ViewModel.BackAsync();

        context.ViewModel.CurrentPath.Value.Should().Be("/");
        context.ViewModel.BackCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task BackCommand_IsObservableAndAwaitable()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();
        await context.ViewModel.OpenFolderAsync(
            context.ViewModel.Items.Single(item => item.Kind == CloudObjectKind.Folder));

        var command = context.ViewModel.BackCommand.Should().BeAssignableTo<IAsyncRelayCommand>().Subject;
        await command.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(5));

        context.ViewModel.CurrentPath.Value.Should().Be("/");
        context.ViewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_ReloadsCurrentFolder()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.RefreshAsync();

        context.Provider.ListedPaths.Should().Equal("/", "/");
        context.ViewModel.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task OpenFile_CacheHitDoesNotOpenProviderThumbnail()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();
        var item = context.ViewModel.Items.Single(candidate => candidate.Kind == CloudObjectKind.Image);
        var cachedPath = Path.GetTempFileName();
        context.Cache.Seed("cloud-thumbnail:3:readme", cachedPath);

        try
        {
            await context.ViewModel.OpenItemAsync(item);

            context.ViewModel.SelectedPreviewPath.Should().Be(cachedPath);
            context.Provider.ThumbnailOpenCount.Should().Be(0);
        }
        finally
        {
            File.Delete(cachedPath);
        }
    }

    [Fact]
    public async Task OpenFile_MissingCachedFileFallsBackToProviderThumbnail()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();
        var item = context.ViewModel.Items.Single(candidate => candidate.Kind == CloudObjectKind.Image);
        context.Cache.Seed("cloud-thumbnail:3:readme", Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg"));

        await context.ViewModel.OpenItemAsync(item);

        context.Provider.ThumbnailOpenCount.Should().Be(1);
        context.Cache.PutRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task OpenFile_CacheMissStoresUnpinnedThumbnailAndDisposesStream()
    {
        using var context = CloudViewModelTestData.Create();
        var stream = new TrackingMemoryStream([1, 2, 3]);
        context.Provider.ThumbnailFactory = (_, _) => Task.FromResult<Stream?>(stream);
        await context.ViewModel.InitializeAsync();
        var item = context.ViewModel.Items.Single(candidate => candidate.Kind == CloudObjectKind.Image);

        await context.ViewModel.OpenItemAsync(item);

        context.Cache.PutRequests.Should().ContainSingle()
            .Which.Should().Be(("cloud-thumbnail:3:readme", false));
        context.ViewModel.SelectedPreviewPath.Should().NotBeNullOrWhiteSpace();
        stream.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task OpenFile_NullThumbnailShowsClearStatus()
    {
        using var context = CloudViewModelTestData.Create();
        context.Provider.ThumbnailFactory = (_, _) => Task.FromResult<Stream?>(null);
        await context.ViewModel.InitializeAsync();
        var item = context.ViewModel.Items.Single(candidate => candidate.Kind == CloudObjectKind.Image);

        await context.ViewModel.OpenItemAsync(item);

        context.ViewModel.SelectedPreviewPath.Should().BeNull();
        context.ViewModel.StatusText.Should().Contain("缩略图");
        context.ViewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task CacheKey_IsolatesProvidersWithSameRemoteId()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();
        var simulated = context.ViewModel.Items.Single(candidate => candidate.Kind == CloudObjectKind.Image);
        var quark = new CloudObjectItemViewModel(CloudViewModelTestData.Item(
            CloudProviderKind.Quark, "readme", "/readme.jpg", "readme.jpg", CloudObjectKind.Image));

        await context.ViewModel.OpenItemAsync(simulated);
        await context.ViewModel.OpenItemAsync(quark);

        context.Cache.RequestedKeys.Should().Contain("cloud-thumbnail:3:readme");
        context.Cache.RequestedKeys.Should().Contain("cloud-thumbnail:1:readme");
    }

    [Fact]
    public async Task ProviderError_IsVisibleAndBusyStateResets()
    {
        using var context = CloudViewModelTestData.Create();
        context.Provider.ListException = new IOException("network unavailable");

        var act = () => context.ViewModel.InitializeAsync();

        await act.Should().NotThrowAsync();
        context.ViewModel.StatusText.Should().Contain("network unavailable");
        context.ViewModel.HasError.Should().BeTrue();
        context.ViewModel.ErrorMessage.Should().Contain("network unavailable");
        context.ViewModel.IsBusy.Should().BeFalse();
        context.ViewModel.IsProgressIndeterminate.Should().BeFalse();
    }

    [Fact]
    public async Task CancelCurrentOperation_StopsSlowLoadAndAllowsLaterRefresh()
    {
        using var context = CloudViewModelTestData.Create();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = true;
        context.Provider.BeforeListAsync = async (_, token) =>
        {
            if (!first)
            {
                return;
            }

            first = false;
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        var initializing = context.ViewModel.InitializeAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        context.ViewModel.CancelCurrentOperationCommand.CanExecute(null).Should().BeTrue();

        context.ViewModel.CancelCurrentOperationCommand.Execute(null);
        await initializing.WaitAsync(TimeSpan.FromSeconds(5));

        context.ViewModel.IsBusy.Should().BeFalse();
        context.ViewModel.StatusText.Should().Contain("取消");
        await context.ViewModel.RefreshAsync();
        context.ViewModel.Items.Should().HaveCount(2);
        context.ViewModel.ProgressValue.Should().Be(1);
    }

    [Fact]
    public async Task FastNavigation_PreventsOlderSlowResultFromReplacingNewerItems()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();
        var folder = context.ViewModel.Items.Single(item => item.Kind == CloudObjectKind.Folder);
        var slowStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSlow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayNextRootListing = true;
        context.Provider.BeforeListAsync = async (path, _) =>
        {
            if (path.Value == "/" && delayNextRootListing)
            {
                delayNextRootListing = false;
                slowStarted.TrySetResult();
                await releaseSlow.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        };
        var slowNavigation = context.ViewModel.RefreshAsync();
        await slowStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var fastNavigation = context.ViewModel.OpenFolderAsync(folder);
        await fastNavigation.WaitAsync(TimeSpan.FromSeconds(5));
        releaseSlow.SetResult();
        await slowNavigation.WaitAsync(TimeSpan.FromSeconds(5));

        context.ViewModel.CurrentPath.Value.Should().Be("/photos");
        context.ViewModel.Items.Should().ContainSingle(item => item.Name == "a.jpg");
    }

    [Fact]
    public async Task SupersededScanner_CannotAppendLateItemsToNewerFolder()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();
        var folder = context.ViewModel.Items.Single(item => item.IsFolder);
        var oldScannerReachedItem = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldScanner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Provider.HonorListCancellation = false;
        context.Provider.BeforeYieldAsync = async (path, _, index, _) =>
        {
            if (path.Value == "/" && index == 0)
            {
                oldScannerReachedItem.TrySetResult();
                await releaseOldScanner.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        };
        var oldRefresh = context.ViewModel.RefreshAsync();
        await oldScannerReachedItem.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await context.ViewModel.OpenFolderAsync(folder).WaitAsync(TimeSpan.FromSeconds(5));
        releaseOldScanner.TrySetResult();
        await oldRefresh.WaitAsync(TimeSpan.FromSeconds(5));

        context.ViewModel.CurrentPath.Value.Should().Be("/photos");
        context.ViewModel.Items.Should().ContainSingle(item => item.Name == "a.jpg");
        context.ViewModel.ScannedItemCount.Should().Be(1);
    }

    [Fact]
    public async Task OpenItemCommand_OpensSelectedFile()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();
        var file = context.ViewModel.Items.Single(item => !item.IsFolder);

        await context.ViewModel.OpenItemCommand.ExecuteAsync(file);

        context.ViewModel.SelectedPreviewPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task NewNavigation_CancelsSlowPreviewAndDisposesItsLateStream()
    {
        using var context = CloudViewModelTestData.Create();
        await context.ViewModel.InitializeAsync();
        var file = context.ViewModel.Items.Single(item => !item.IsFolder);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new TrackingMemoryStream([1, 2, 3]);
        context.Provider.ThumbnailFactory = async (_, _) =>
        {
            started.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return stream;
        };
        var preview = context.ViewModel.OpenItemAsync(file);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await context.ViewModel.RefreshAsync();
        release.SetResult();
        await preview.WaitAsync(TimeSpan.FromSeconds(5));

        context.ViewModel.SelectedPreviewPath.Should().BeNull();
        context.ViewModel.Items.Should().HaveCount(2);
        stream.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task Back_UsesImmediateParentForNestedFolder()
    {
        var subFolder = CloudViewModelTestData.Item(
            CloudProviderKind.Simulated, "sub", "/photos/sub", "sub", CloudObjectKind.Folder);
        var provider = new StubCloudProvider(new Dictionary<string, IReadOnlyList<CloudObject>>
        {
            ["/"] =
            [CloudViewModelTestData.Item(
                CloudProviderKind.Simulated, "photos", "/photos", "photos", CloudObjectKind.Folder)],
            ["/photos"] = [subFolder],
            ["/photos/sub"] = []
        });
        using var cache = new MemoryCache();
        using var vm = new CloudHubViewModel(
            provider,
            new MemoryIndex(),
            cache,
            new TrackingSynchronizationContext());
        await vm.InitializeAsync();
        await vm.OpenFolderAsync(vm.Items.Single());
        await vm.OpenFolderAsync(vm.Items.Single());

        await vm.BackAsync();

        vm.CurrentPath.Value.Should().Be("/photos");
        vm.Items.Should().ContainSingle(item => item.Name == "sub");
    }

    [Fact]
    public async Task AsyncPropertyUpdates_AreMarshaledThroughCapturedSynchronizationContext()
    {
        var synchronizationContext = new TrackingSynchronizationContext();
        using var context = CloudViewModelTestData.Create(synchronizationContext);

        var observedOnCapturedContext = false;
        context.ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CloudHubViewModel.StatusText))
            {
                observedOnCapturedContext |= ReferenceEquals(
                    SynchronizationContext.Current,
                    synchronizationContext);
            }
        };

        await context.ViewModel.InitializeAsync();

        synchronizationContext.PostCount.Should().BeGreaterThan(0);
        observedOnCapturedContext.Should().BeTrue();
    }

    [Fact]
    public async Task DispatcherFailure_DoesNotEscapeOrLeaveOperationBusy()
    {
        using var context = CloudViewModelTestData.Create(new ThrowingSynchronizationContext());

        var act = () => context.ViewModel.InitializeAsync();

        await act.Should().NotThrowAsync();
        context.ViewModel.IsBusy.Should().BeFalse();
        context.ViewModel.HasError.Should().BeTrue();
        context.ViewModel.ErrorMessage.Should().Contain("dispatcher unavailable");
    }

    [Fact]
    public void Constructor_WithoutExplicitOrCurrentUiContextThrowsClearly()
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            using var context = CloudViewModelTestData.Create();
            var act = () => new CloudHubViewModel(
                context.Provider,
                context.Index,
                context.Cache,
                null);

            act.Should().Throw<InvalidOperationException>().WithMessage("*UI*");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public async Task AllObservableUpdates_RunOnExplicitDedicatedUiContext()
    {
        using var uiContext = new DedicatedThreadSynchronizationContext();
        using var context = CloudViewModelTestData.Create(uiContext);
        var observedThreads = new ConcurrentQueue<int>();
        context.ViewModel.PropertyChanged += (_, _) =>
            observedThreads.Enqueue(Environment.CurrentManagedThreadId);
        context.ViewModel.Items.CollectionChanged += (_, _) =>
            observedThreads.Enqueue(Environment.CurrentManagedThreadId);

        await context.ViewModel.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));

        observedThreads.Should().NotBeEmpty();
        observedThreads.Should().OnlyContain(threadId => threadId == uiContext.ThreadId);
    }

    [Fact]
    public async Task Dispose_CancelsActiveOperationAndLeavesViewModelIdle()
    {
        using var context = CloudViewModelTestData.Create();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Provider.BeforeListAsync = async (_, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        var initializing = context.ViewModel.InitializeAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        context.ViewModel.Should().BeAssignableTo<IDisposable>();
        ((IDisposable)context.ViewModel).Dispose();
        await initializing.WaitAsync(TimeSpan.FromSeconds(5));

        context.ViewModel.IsBusy.Should().BeFalse();
        context.ViewModel.StatusText.Should().Contain("取消");
    }
}
