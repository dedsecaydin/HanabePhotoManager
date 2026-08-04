# 微信九图批量发送 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在“图片小工具”中加入可取消、每批最多 9 张、仅明确失败才有限重试的 Windows 微信原图文件发送功能。

**Architecture:** 微信页面使用独立 `WeChatSenderViewModel`，避免复用压缩队列。纯 `WeChatSendQueueService` 负责分批和状态机，`IWeChatDesktopGateway` 隔离所有 Windows 进程、前台窗口与 UI Automation；测试只注入 fake gateway，永不操作真实微信。

**Tech Stack:** .NET 8、WPF、CommunityToolkit.Mvvm、Windows P/Invoke、Windows UI Automation、xUnit、FluentAssertions。

## Global Constraints

- 不调用非官方微信 API，不读取 Cookie，不绕过登录、验证码或安全提示。
- 原图通过文件附件路径发送，不转码、不压缩、不制作临时副本。
- 每批最多 9 张；首次发送后，每个明确失败文件最多重试 3 次，退避 1/2/4 秒。
- `Ambiguous` 项绝不自动重试；发送目标必须二次确认。
- 每个有副作用动作前均校验微信处于前台且 PID 属于已验证微信进程。
- 用户可取消；取消后不开始新批次或重试。
- 非 Windows 平台禁用自动发送。
- 普通自动化测试不得调用真实微信。
- 不修改 `MainWindow.xaml` 或 `MainWindowViewModel.cs`。
- 不 commit、不 push。

---

### Task 1: 队列领域模型与分批

**Files:**
- Create: `src/HanabePhotoManager.App/WeChat/WeChatSendModels.cs`
- Create: `src/HanabePhotoManager.App/WeChat/WeChatBatchPlanner.cs`
- Test: `tests/HanabePhotoManager.App.Tests/WeChat/WeChatBatchPlannerTests.cs`

**Interfaces:**
- Produces: `WeChatSendItem`, `WeChatSendItemState`, `WeChatSendBatch`, `WeChatBatchPlanner.Create(IReadOnlyList<WeChatSendItem>)`.

- [ ] **Step 1: Write failing batch tests**

```csharp
[Fact]
public void Create_SplitsNineteenItemsIntoNineNineOne()
{
    var batches = WeChatBatchPlanner.Create(CreateItems(19));
    batches.Select(x => x.Items.Count).Should().Equal(9, 9, 1);
}

[Fact]
public void Create_DoesNotPlaceDuplicateDisplayNamesInOneBatch()
{
    var batches = WeChatBatchPlanner.Create([
        Item(@"C:\a\same.jpg"), Item(@"C:\b\same.jpg"), Item(@"C:\c\other.jpg")]);
    batches.Should().OnlyContain(batch =>
        batch.Items.Select(x => x.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == batch.Items.Count);
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj --filter FullyQualifiedName~WeChatBatchPlannerTests --artifacts-path .artifacts/wechat-red-1
```

Expected: FAIL because `WeChatBatchPlanner` and models do not exist.

- [ ] **Step 3: Implement immutable queue items and deterministic batches**

Create records/enums with immutable queue ID, normalized source path, display name, length, write time, attempt count and state. Implement a stable first-fit planner with capacity 9 and case-insensitive unique names per batch.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Step 2 command with `--artifacts-path .artifacts/wechat-green-1`.

Expected: all `WeChatBatchPlannerTests` pass.

### Task 2: Queue state machine, success evidence and retry isolation

**Files:**
- Create: `src/HanabePhotoManager.App/WeChat/IWeChatDesktopGateway.cs`
- Create: `src/HanabePhotoManager.App/WeChat/WeChatSendQueueService.cs`
- Test: `tests/HanabePhotoManager.App.Tests/WeChat/WeChatSendQueueServiceTests.cs`

**Interfaces:**
- Consumes: `WeChatSendBatch`, `WeChatSendItem`.
- Produces: `IWeChatDesktopGateway.EnsureReadyAsync`, `ConfirmTargetAsync`, `SendBatchAsync`; `WeChatBatchSendResult`; `WeChatSendQueueService.SendAsync`.

- [ ] **Step 1: Write failing state-machine tests**

```csharp
[Fact]
public async Task SendAsync_SendsNineAtATimeAndRetriesOnlyExplicitFailures()
{
    var gateway = new FakeGateway(
        BatchResult.SuccessExcept("p5.jpg"),
        BatchResult.Success("p5.jpg"));
    var result = await Service(gateway).SendAsync(CreateItems(10), ConfirmedTarget(), null, CancellationToken.None);
    gateway.Calls.Select(x => x.Select(i => i.DisplayName)).Should().Equal(
        CreateItems(9).Select(i => i.DisplayName),
        new[] { "p5.jpg" },
        new[] { "p10.jpg" });
    result.Items.Should().OnlyContain(x => x.State == WeChatSendItemState.Sent);
}

[Fact]
public async Task SendAsync_DoesNotRetryAmbiguousItems()
{
    var gateway = new FakeGateway(BatchResult.Ambiguous("p1.jpg"));
    var result = await Service(gateway).SendAsync(CreateItems(1), ConfirmedTarget(), null, CancellationToken.None);
    gateway.Calls.Should().HaveCount(1);
    result.IsPaused.Should().BeTrue();
}
```

Add focused tests for cancellation, front-window/target rejection, three retries, partial success and progress monotonicity.

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj --filter FullyQualifiedName~WeChatSendQueueServiceTests --artifacts-path .artifacts/wechat-red-2
```

Expected: FAIL because gateway and queue service are missing.

- [ ] **Step 3: Implement minimal serialized state machine**

The service must:

```csharp
foreach (var batch in WeChatBatchPlanner.Create(items))
{
    await gateway.EnsureReadyAsync(target, token);
    var pending = batch.Items;
    for (var attempt = 0; pending.Count > 0 && attempt <= 3; attempt++)
    {
        var result = await gateway.SendBatchAsync(pending, target, token);
        MarkSentAndFailed(result);
        if (result.HasAmbiguous) return PausedResult();
        pending = ExplicitFailuresOnly(result);
        if (pending.Count > 0) await delay(TimeSpan.FromSeconds(1 << attempt), token);
    }
}
```

Use an injected delay delegate in tests so retry tests remain instant. Never derive success from an empty input box alone; the gateway result requires `InputCleared`, a new filename bubble, completed upload, no failure marker and unchanged target.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Step 2 command with `--artifacts-path .artifacts/wechat-green-2`.

Expected: all queue-service tests pass.

### Task 3: Windows gateway and executable locator

**Files:**
- Create: `src/HanabePhotoManager.App/WeChat/WeChatExecutableLocator.cs`
- Create: `src/HanabePhotoManager.App/WeChat/WindowsWeChatDesktopGateway.cs`
- Create: `src/HanabePhotoManager.App/WeChat/WeChatNativeMethods.cs`
- Modify: `src/HanabePhotoManager.App/HanabePhotoManager.App.csproj`
- Test: `tests/HanabePhotoManager.App.Tests/WeChat/WeChatExecutableLocatorTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/WeChat/WeChatForegroundVerifierTests.cs`

**Interfaces:**
- Consumes: `IWeChatDesktopGateway`.
- Produces: `IWeChatExecutableLocator.Locate`, `IWeChatWindowApi`, `WeChatForegroundVerifier.IsVerifiedForeground`.

- [ ] **Step 1: Write failing locator and PID-verifier tests**

Tests provide fake filesystem/registry/process-window adapters. Assert accepted executable names, multiple-candidate ambiguity, launcher PID handoff, foreground PID mismatch, minimized restore failure and non-Windows unsupported status.

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj --filter "FullyQualifiedName~WeChatExecutableLocatorTests|FullyQualifiedName~WeChatForegroundVerifierTests" --artifacts-path .artifacts/wechat-red-3
```

Expected: FAIL because locator/verifier types are missing.

- [ ] **Step 3: Implement Windows-only bounded gateway**

Add UI Automation references:

```xml
<Reference Include="UIAutomationClient" />
<Reference Include="UIAutomationTypes" />
```

Implement:

- process discovery for `Weixin.exe` and `WeChat.exe`;
- configured path, App Paths and common-directory lookup;
- 20-second visible-window wait;
- `ShowWindow(SW_RESTORE)`, `SetForegroundWindow`, `GetForegroundWindow`, `GetWindowThreadProcessId`;
- UI Automation target-title/input discovery with bounded timeout;
- `CF_HDROP` clipboard file list and a single paste/send action only after target/foreground/previews pass;
- post-send read-only evidence classification into success, explicit failure or ambiguous.

All platform entry points first check `OperatingSystem.IsWindows()`. Any unsupported UI tree or uncertain evidence returns `Ambiguous`/paused, never blind coordinates or automatic retry.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Step 2 command with `--artifacts-path .artifacts/wechat-green-3`.

Expected: locator and foreground-verifier tests pass without opening微信.

### Task 4: ViewModel and WPF integration

**Files:**
- Create: `src/HanabePhotoManager.App/WeChat/WeChatSenderViewModel.cs`
- Create: `src/HanabePhotoManager.App/WeChat/WeChatSenderView.xaml`
- Create: `src/HanabePhotoManager.App/WeChat/WeChatSenderView.xaml.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/CompressionViewModel.cs`
- Modify: `src/HanabePhotoManager.App/Compression/CompressionPage.xaml`
- Modify: `src/HanabePhotoManager.App/Compression/CompressionPage.xaml.cs`
- Test: `tests/HanabePhotoManager.App.Tests/WeChat/WeChatSenderViewModelTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/WeChat/WeChatSenderViewStructureTests.cs`

**Interfaces:**
- Consumes: `WeChatSendQueueService`, `IWeChatDesktopGateway`, `ImageInputDiscovery`.
- Produces: `WeChatSenderViewModel.AddInputs`, `DetectCommand`, `LocateTargetCommand`, `ConfirmTargetCommand`, `StartCommand`, `CancelCommand`.

- [ ] **Step 1: Write failing ViewModel/UI tests**

Assert:

- `ImageToolMode.WeChatSend` and horizontal tab label exist;
- queue is independent from compression items;
- start requires files, ready gateway and confirmed target;
- changing target invalidates confirmation;
- progress/counters update and cancellation flows to service;
- WPF contains progress bar, cancel button, target confirmation details and failed/ambiguous list.

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj --filter "FullyQualifiedName~WeChatSender|FullyQualifiedName~CompressionViewModelTests" --artifacts-path .artifacts/wechat-red-4
```

Expected: FAIL because the mode, ViewModel and view are missing.

- [ ] **Step 3: Implement the ViewModel and view**

Add a fourth smooth-fade grid for `WeChatSenderView`. Route drop/choose events by selected mode. Bind:

- queue list and item states;
- target input/read-back title/type;
- explicit “确认此目标” command;
- environment/status text;
- progress bar and sent/failed/ambiguous counters;
- start and cancel commands.

Construct the production gateway only inside the Windows-enabled ViewModel factory path. Tests inject fake gateway and fake queue service.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Step 2 command with `--artifacts-path .artifacts/wechat-green-4`.

Expected: all focused tests pass.

### Task 5: Verification and regression

**Files:**
- Modify only files already listed if verification exposes a tested defect.

- [ ] **Step 1: Run all App tests**

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj --artifacts-path .artifacts/wechat-app-tests
```

Expected: all tests pass; no real WeChat process is started.

- [ ] **Step 2: Run complete solution tests**

```powershell
dotnet test HanabePhotoManager.sln --artifacts-path .artifacts/wechat-full-tests
```

Expected: all tests pass.

- [ ] **Step 3: Build release output in an isolated artifacts directory**

```powershell
dotnet build src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release --artifacts-path .artifacts/wechat-release
```

Expected: build succeeds with zero errors and zero new warnings.

- [ ] **Step 4: Inspect Git status without committing**

```powershell
git status --short
```

Expected: only requested/local pre-existing changes are present; no commit or push is performed.
