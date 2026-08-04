# Unified Cloud Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge Baidu and Quark into one cloud navigation destination with an accessible horizontal provider switch, preserved WebView2 sessions, smooth switching, and reversible embedded-page dark mode.

**Architecture:** `MainWindowViewModel` owns the canonical `Cloud` destination and selected provider while compatibility commands forward old entry points. `MainWindow` continues to host two long-lived `CloudPage` instances, activates only the selected one, and animates transitions. `CloudPage` maps the application theme to WebView2 profile preferences and a removable, media-safe fallback stylesheet.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm, Microsoft.Web.WebView2 1.0.4078.44, xUnit, FluentAssertions.

## Global Constraints

- Keep both existing WebView2 user-data directory names and login sessions.
- Do not merge cookies or cache.
- Do not use page-wide `filter: invert`.
- Preserve old cloud commands and migrate old navigation settings.
- Do not modify compression or date-loading code.
- Do not commit or push.

---

### Task 1: Canonical Cloud Navigation and Migration

**Files:**
- Modify: `tests/HanabePhotoManager.App.Tests/Cloud/MainWindowViewModelNavigationTests.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/NavigationOrderPolicyTests.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/Navigation/NavigationOrderPolicy.cs`

**Interfaces:**
- Produces: `CloudProviderChoice`, `SelectedCloudProvider`, `IsCloudPage`, `SelectCloudProviderCommand`, `ShowCloudCommand`.
- Preserves: `ShowBaiduCloudCommand`, `ShowQuarkCloudCommand`, `IsBaiduCloudPage`, `IsQuarkCloudPage`.

- [ ] **Step 1: Write failing navigation tests**

Add tests proving old commands end at `CurrentPage == "Cloud"`, select the correct provider, title is “网盘”, and default navigation contains one `Cloud`.

- [ ] **Step 2: Write failing migration tests**

Add cases:

```csharp
[InlineData(new[] { "Home", "QuarkCloud", "Preview", "BaiduCloud" },
            new[] { "Home", "Cloud", "Preview" })]
[InlineData(new[] { "Home", "Cloud", "BaiduCloud", "Cloud" },
            new[] { "Home", "Cloud" })]
```

- [ ] **Step 3: Verify RED**

Run:

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj `
  -c Release --filter "FullyQualifiedName~Cloud|FullyQualifiedName~NavigationOrderPolicy" `
  -p:BaseOutputPath=.artifacts/cloud-red/bin/ `
  -p:BaseIntermediateOutputPath=.artifacts/cloud-red/obj/
```

Expected: failures because `CloudProviderChoice`, `ShowCloudCommand`, and old-key migration do not exist.

- [ ] **Step 4: Implement minimal canonical navigation**

Add the provider enum and properties, make old commands select a provider then call the canonical destination, replace the default two navigation keys with `Cloud`, and map its label/icon/title/subtitle.

- [ ] **Step 5: Implement idempotent migration**

Normalize `BaiduCloud`, `QuarkCloud`, and duplicate `Cloud` keys to a single `Cloud` at the earliest legacy position before filling missing defaults.

- [ ] **Step 6: Verify GREEN**

Re-run the filtered command and expect all selected tests to pass.

### Task 2: Unified Page and Smooth Provider Switch

**Files:**
- Modify: `tests/HanabePhotoManager.App.Tests/Cloud/CloudPageTests.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `SelectedCloudProvider`, `IsCloudPage`, and provider selection properties from Task 1.
- Produces: one visible Cloud page surface containing two preserved `CloudPage` hosts.

- [ ] **Step 1: Write failing structure tests**

Assert XAML contains one `CloudPageContainer`, horizontal Baidu/Quark selector buttons bound to the provider command, both unchanged `InitialUrl` values, and only the selected host has `IsActive=true`.

- [ ] **Step 2: Verify RED**

Run the Cloud and theme test filter using `.artifacts/cloud-switch-red`; expect failure because the unified container does not exist.

- [ ] **Step 3: Implement the unified surface**

Replace the two destination-level cloud elements with one `IsCloudPage` surface. Keep both named `CloudPage` instances under the surface and preserve their URLs.

- [ ] **Step 4: Implement provider transition**

Use a 180ms opacity transition. Cancel any previous animation, make the incoming host visible before activation, and deactivate/hide the outgoing host after completion. If `SystemParameters.ClientAreaAnimation` is false, switch immediately.

- [ ] **Step 5: Update page animation and disposal**

Map `CurrentPage == "Cloud"` to the unified container. Continue disposing both WebView2 instances when the window closes.

- [ ] **Step 6: Verify GREEN**

Run the filtered tests and expect all selected tests to pass.

### Task 3: Reversible WebView2 Dark Theme

**Files:**
- Modify: `tests/HanabePhotoManager.App.Tests/ThemeManagerTests.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/Cloud/CloudPageTests.cs`
- Modify: `src/HanabePhotoManager.App/Services/ThemeManager.cs`
- Modify: `src/HanabePhotoManager.App/Cloud/CloudPage.xaml.cs`

**Interfaces:**
- Produces: `ThemeManager.ThemeChanged`; `CloudPage.IsDarkTheme`; one stored document-created script id.
- Consumes: `CoreWebView2.Profile.PreferredColorScheme`, script add/remove APIs.

- [ ] **Step 1: Write failing theme notification tests**

Test that a changed theme emits one notification and repeated application of the same theme emits none.

- [ ] **Step 2: Write failing CloudPage contract tests**

Assert `IsDarkTheme` is a dependency property and the fallback stylesheet source:

- uses a fixed `hanabe-cloud-dark-style` id;
- has no page-wide `filter: invert`;
- excludes `img`, `picture`, `video`, `canvas`, `svg`, `iframe`, `[role=img]`, QR and CAPTCHA selectors;
- supports removal when light theme returns.

- [ ] **Step 3: Verify RED**

Run the Cloud and ThemeManager filters using `.artifacts/cloud-theme-red`; expect missing event/property/style failures.

- [ ] **Step 4: Implement theme event and binding**

Raise `ThemeChanged` only after `Current` actually changes. Bind both `CloudPage.IsDarkTheme` values to the application theme state, including settings-page theme changes.

- [ ] **Step 5: Implement WebView2 native color preference**

After initialization and on every theme change set the profile to explicit `Dark` or `Light`; retain the latest requested value if WebView2 is not initialized or is suspended.

- [ ] **Step 6: Implement safe fallback script**

Register one document-created script, store its id, and update the current document immediately. Dark mode inserts the fixed style; light mode removes it. The stylesheet changes backgrounds/text/borders/forms but never globally inverts content and excludes media/security verification trees.

- [ ] **Step 7: Implement lifecycle safety**

Serialize theme applications with a version guard, tolerate initialization/navigation/disposal races, remove the registered script and unsubscribe events in `Dispose`.

- [ ] **Step 8: Verify GREEN**

Re-run the filtered tests and expect all selected tests to pass.

### Task 4: Onboarding, Regression, and Build

**Files:**
- Modify: `tests/HanabePhotoManager.App.Tests/OnboardingTests.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`

**Interfaces:**
- Consumes: compatibility commands and `SelectedCloudProvider`.
- Produces: two onboarding steps that share the unified page while selecting different providers.

- [ ] **Step 1: Write failing onboarding tests**

Prove the Baidu step enters `Cloud` with Baidu selected and the Quark step enters `Cloud` with Quark selected while step count remains unchanged.

- [ ] **Step 2: Verify RED**

Run the onboarding filter using `.artifacts/cloud-onboarding-red`; expect old page names.

- [ ] **Step 3: Implement onboarding forwarding**

Set the requested provider before assigning `CurrentPage = "Cloud"` for steps 12 and 13.

- [ ] **Step 4: Run targeted tests**

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj `
  -c Release --filter "FullyQualifiedName~Cloud|FullyQualifiedName~NavigationOrderPolicy|FullyQualifiedName~ThemeManager|FullyQualifiedName~Onboarding" `
  -p:BaseOutputPath=.artifacts/cloud-targeted/bin/ `
  -p:BaseIntermediateOutputPath=.artifacts/cloud-targeted/obj/
```

Expected: all selected tests pass.

- [ ] **Step 5: Run full tests**

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj `
  -c Release `
  -p:BaseOutputPath=.artifacts/cloud-full/bin/ `
  -p:BaseIntermediateOutputPath=.artifacts/cloud-full/obj/
```

Expected: all tests pass.

- [ ] **Step 6: Build isolated release**

```powershell
dotnet build src/HanabePhotoManager.App/HanabePhotoManager.App.csproj `
  -c Release `
  -p:BaseOutputPath=.artifacts/cloud-build/bin/ `
  -p:BaseIntermediateOutputPath=.artifacts/cloud-build/obj/
```

Expected: zero errors and zero warnings.

- [ ] **Step 7: Verify repository state**

Run `git diff --check` and `git status --short`; expect no whitespace errors and no commit/push.
