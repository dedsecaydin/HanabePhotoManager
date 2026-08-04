# Date Open Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a cancellable, cached, single-pass date-directory snapshot service plus a one-reset range collection so the main view model can move network I/O off the UI thread and publish bounded batches.

**Architecture:** `LibraryDateSnapshotService` scans each configured category once on a worker thread, emits immutable 64-item progress batches, caches the three most recent stable snapshots using directory fingerprints, and exposes a separate cancellable recursive-capacity operation. `RangeObservableCollection<T>` lets the later main-line integration add or replace a batch with one collection reset instead of per-item notifications.

**Tech Stack:** .NET 8, C# 12, WPF `ObservableCollection<T>`, xUnit, FluentAssertions

## Global Constraints

- Do not modify `MainWindow.xaml`, `MainWindowViewModel.cs`, Cloud, or Compression files in this task.
- Do not modify, rename, move, or decode user photos.
- Use one `FileInfo`-backed property snapshot per discovered file.
- Cancellation propagates as `OperationCanceledException`; partial I/O failures return warnings and `IsPartial = true`.
- Cache only stable, complete snapshots; retain at most the three most recently used dates.
- Do not commit or push.

---

### Task 1: Immutable snapshot contracts and single-pass scanner

**Files:**
- Create: `src/HanabePhotoManager.App/Models/LibraryDateSnapshot.cs`
- Create: `src/HanabePhotoManager.App/Services/LibraryDateSnapshotService.cs`
- Test: `tests/HanabePhotoManager.App.Tests/LibraryDateSnapshotServiceTests.cs`

**Interfaces:**
- Produces:
  - `Task<LibraryDateSnapshot> LoadAsync(string dateDirectory, CancellationToken cancellationToken = default)`
  - `Task<LibraryDateSnapshot> LoadAsync(string dateDirectory, IProgress<LibraryDateSnapshotBatch>? progress, CancellationToken cancellationToken = default)`
  - `Task<LibraryDirectoryCapacityResult> CalculateCapacityAsync(string directory, CancellationToken cancellationToken = default)`
  - `void Invalidate(string dateDirectory)`
  - `void ClearCache()`

- [ ] **Step 1: Write failing scanner tests**

```csharp
[Fact]
public async Task LoadAsync_EnumeratesEachCategoryOnceAndAggregatesTheSameSnapshots()
{
    var fileSystem = FakeLibraryDateFileSystem.WithFiles(
        ("JPG生图", "a.jpg", 12L),
        ("JPG生图", "b.jpg", 30L));
    var service = new LibraryDateSnapshotService(fileSystem);

    var result = await service.LoadAsync(fileSystem.Root);

    result.Items.Should().HaveCount(2);
    result.Categories.Single(x => x.Name == "JPG生图").TotalBytes.Should().Be(42);
    fileSystem.TopLevelEnumerationCounts["JPG生图"].Should().Be(1);
}
```

- [ ] **Step 2: Run the scanner test and verify RED**

Run:

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj --filter FullyQualifiedName~LibraryDateSnapshotServiceTests
```

Expected: compilation fails because `LibraryDateSnapshotService` and snapshot contracts do not exist.

- [ ] **Step 3: Implement immutable models and minimal scanner**

```csharp
public sealed record LibraryDateMediaItem(
    string FullPath,
    string Name,
    string Extension,
    string Category,
    long Length,
    DateTime LastWriteTimeUtc);

public sealed record LibraryDateSnapshot(
    string DateDirectory,
    IReadOnlyList<LibraryDateMediaItem> Items,
    IReadOnlyList<LibraryDateCategorySnapshot> Categories,
    IReadOnlyList<LibraryDateSnapshotWarning> Warnings,
    bool IsPartial,
    string Fingerprint,
    DateTime CreatedUtc);
```

The physical file-system adapter must yield a completed `LibraryDateFileProperties` value from each enumerated `FileInfo`; the service must aggregate category counts and bytes from that value without reopening the file path.

- [ ] **Step 4: Run the scanner tests and verify GREEN**

Run the command from Step 2.

Expected: all `LibraryDateSnapshotServiceTests` scanner tests pass.

### Task 2: Cancellation, partial failures, 64-item progress, and LRU

**Files:**
- Modify: `src/HanabePhotoManager.App/Models/LibraryDateSnapshot.cs`
- Modify: `src/HanabePhotoManager.App/Services/LibraryDateSnapshotService.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/LibraryDateSnapshotServiceTests.cs`

**Interfaces:**
- Consumes: the Task APIs from Task 1.
- Produces: stable three-entry LRU behavior, fingerprint invalidation, and `LibraryDateSnapshotBatch`.

- [ ] **Step 1: Write failing behavior tests**

```csharp
[Fact]
public async Task LoadAsync_WhenCancelled_ThrowsWithoutCachingPartialResult()
{
    using var cancellation = new CancellationTokenSource();
    var fileSystem = FakeLibraryDateFileSystem.CancelsDuringEnumeration(cancellation);
    var service = new LibraryDateSnapshotService(fileSystem);

    var act = () => service.LoadAsync(fileSystem.Root, cancellation.Token);

    await act.Should().ThrowAsync<OperationCanceledException>();
    service.CachedSnapshotCount.Should().Be(0);
}

[Fact]
public async Task LoadAsync_UsesThreeEntryLruAndInvalidatesChangedFingerprint()
{
    // Load A, B, C, touch A, load D, then alter A's category stamp.
    // B is evicted; A is rescanned after its fingerprint changes.
}
```

- [ ] **Step 2: Run the tests and verify RED**

Expected: tests fail because cancellation isolation, batching, and cache behavior are absent.

- [ ] **Step 3: Add minimal behavior**

Implement:

- cancellation checks before and during every enumeration;
- batches capped at 64;
- warnings for category/file failures;
- no cache write for partial or changing-directory snapshots;
- a lock-protected `Dictionary + LinkedList` LRU capped at three paths;
- a fingerprint from the date root and six category directory stamps;
- fingerprint recheck before cache insertion.

- [ ] **Step 4: Run the tests and verify GREEN**

Run:

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj --filter FullyQualifiedName~LibraryDateSnapshotServiceTests
```

Expected: all snapshot service tests pass.

### Task 3: Deferred recursive-capacity result

**Files:**
- Modify: `src/HanabePhotoManager.App/Models/LibraryDateSnapshot.cs`
- Modify: `src/HanabePhotoManager.App/Services/LibraryDateSnapshotService.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/LibraryDateSnapshotServiceTests.cs`

**Interfaces:**
- Produces: `LibraryDirectoryCapacityResult` with `TotalBytes`, `FilesVisited`, `Warnings`, and `IsPartial`.

- [ ] **Step 1: Write failing capacity tests**

```csharp
[Fact]
public async Task CalculateCapacityAsync_ReturnsUsableTotalWhenOneEntryFails()
{
    var fileSystem = FakeLibraryDateFileSystem.WithRecursiveFailure(
        goodLengths: [10L, 20L],
        failedPath: "locked.bin");

    var result = await new LibraryDateSnapshotService(fileSystem)
        .CalculateCapacityAsync(fileSystem.Root);

    result.TotalBytes.Should().Be(30);
    result.FilesVisited.Should().Be(2);
    result.IsPartial.Should().BeTrue();
    result.Warnings.Should().ContainSingle();
}
```

- [ ] **Step 2: Run the test and verify RED**

Expected: the capacity test fails because the capacity contract is missing or incomplete.

- [ ] **Step 3: Implement the independent capacity operation**

Run recursive enumeration entirely inside `Task.Run`, check cancellation per entry, preserve accumulated bytes on ordinary I/O failures, and propagate cancellation without converting it into a partial result.

- [ ] **Step 4: Run the tests and verify GREEN**

Expected: all snapshot and capacity tests pass.

### Task 4: One-reset range collection

**Files:**
- Create: `src/HanabePhotoManager.App/Collections/RangeObservableCollection.cs`
- Create: `tests/HanabePhotoManager.App.Tests/RangeObservableCollectionTests.cs`

**Interfaces:**
- Produces:
  - `void AddRange(IEnumerable<T> items)`
  - `void ReplaceRange(IEnumerable<T> items)`

- [ ] **Step 1: Write failing collection notification tests**

```csharp
[Fact]
public void AddRange_RaisesOneResetForTheWholeBatch()
{
    var collection = new RangeObservableCollection<int>();
    var events = new List<NotifyCollectionChangedEventArgs>();
    collection.CollectionChanged += (_, args) => events.Add(args);

    collection.AddRange([1, 2, 3]);

    events.Should().ContainSingle()
        .Which.Action.Should().Be(NotifyCollectionChangedAction.Reset);
}
```

- [ ] **Step 2: Run and verify RED**

Expected: compilation fails because `RangeObservableCollection<T>` does not exist.

- [ ] **Step 3: Implement minimal range mutations**

Mutate `Items` under `CheckReentrancy()`, then raise `Count`, indexer, and one collection `Reset`. Empty `AddRange` performs no notification.

- [ ] **Step 4: Run and verify GREEN**

Run:

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj --filter "FullyQualifiedName~RangeObservableCollectionTests|FullyQualifiedName~LibraryDateSnapshotServiceTests"
```

Expected: all new functional tests pass.

### Task 5: 2,000-file performance guard

**Files:**
- Create: `tests/HanabePhotoManager.App.Tests/LibraryDateSnapshotPerformanceTests.cs`

**Interfaces:**
- Consumes: `LibraryDateSnapshotService.LoadAsync`.
- Produces: a repeatable local-filesystem performance guard and batch-size assertion.

- [ ] **Step 1: Write the performance test**

Create 2,000 zero-content `.jpg` files in a temporary `JPG生图` directory, time a load, and assert:

```csharp
snapshot.Items.Should().HaveCount(2_000);
batches.Should().OnlyContain(batch => batch.Items.Count <= 64);
stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
```

- [ ] **Step 2: Run the performance test**

Run:

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj --filter FullyQualifiedName~LibraryDateSnapshotPerformanceTests
```

Expected: PASS on local SSD without touching the user library.

- [ ] **Step 3: Run the complete app test project**

Run:

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj
```

Expected: all tests pass with no new warnings.

- [ ] **Step 4: Inspect status without committing**

Run:

```powershell
git status --short
```

Expected: only the intended uncommitted files from this and the other approved parallel work are present. Do not commit or push.
