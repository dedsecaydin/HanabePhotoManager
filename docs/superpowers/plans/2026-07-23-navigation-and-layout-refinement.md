# Navigation and Layout Refinement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver reorderable icon-aware first-level navigation and correct the Browse, Settings, Map, Compression, Connected Devices, and brand-image layouts without changing business workflows.

**Architecture:** Add a focused App-layer navigation model/policy and persist stable navigation keys through the existing `AppSettingsStore`. Keep WPF drag mechanics in `MainWindow.xaml.cs`, state and persistence in `MainWindowViewModel`, and visual rules in shared theme dictionaries before consuming them from pages.

**Tech Stack:** .NET 8, C# 12, WPF/XAML, CommunityToolkit.Mvvm, xUnit, FluentAssertions.

## Global Constraints

- Calendar behavior and thumbnail-size controls must not change.
- Theme switching and Settings stay fixed in the sidebar footer and are not reorderable.
- Use project semantic brushes, size tokens, and shared control styles; no raw page colors or third-party UI dependency.
- Icon-only navigation must keep tooltips, automation names, visible keyboard focus, and keyboard activation.
- Existing page commands, bindings, and business data flows remain intact.

---

### Task 1: Navigation State and Persistence

**Files:**
- Create: `src/HanabePhotoManager.App/Navigation/NavigationDisplayMode.cs`
- Create: `src/HanabePhotoManager.App/Navigation/NavigationItemViewModel.cs`
- Create: `src/HanabePhotoManager.App/Navigation/NavigationOrderPolicy.cs`
- Modify: `src/HanabePhotoManager.App/Services/AppSettingsStore.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Test: `tests/HanabePhotoManager.App.Tests/NavigationOrderPolicyTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/AppSettingsStoreTests.cs`

**Interfaces:**
- Produces: `NavigationDisplayMode`, `NavigationItemViewModel`, `NavigationOrderPolicy.Normalize(IEnumerable<string>?, IReadOnlyList<string>)`, `MainWindowViewModel.NavigationItems`, `MoveNavigationItem(string sourceKey, string targetKey)`.

- [ ] **Step 1: Write failing normalization and persistence tests**

```csharp
[Fact]
public void Normalize_RemovesUnknownAndDuplicateKeys_ThenAppendsMissingDefaults()
{
    var result = NavigationOrderPolicy.Normalize(["Preview", "Unknown", "Preview", "Home"], ["Home", "Import", "Preview"]);
    result.Should().Equal("Preview", "Home", "Import");
}

[Fact]
public async Task NavigationPreferencesSurviveRestart()
{
    await store.SaveAsync(new AppSettings { NavigationOrder = ["Preview", "Home"], NavigationDisplayMode = "Icon" });
    var loaded = await store.LoadAsync();
    loaded.NavigationOrder.Should().Equal("Preview", "Home");
    loaded.NavigationDisplayMode.Should().Be("Icon");
}
```

- [ ] **Step 2: Run focused tests and confirm failure**

Run: `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter "FullyQualifiedName~NavigationOrderPolicyTests|FullyQualifiedName~NavigationPreferencesSurviveRestart"`

Expected: FAIL because navigation types and settings properties do not exist.

- [ ] **Step 3: Implement normalized stable-key state**

```csharp
public enum NavigationDisplayMode { Text, Icon, IconAndText }

public static IReadOnlyList<string> Normalize(IEnumerable<string>? stored, IReadOnlyList<string> defaults)
{
    var allowed = defaults.ToHashSet(StringComparer.Ordinal);
    var result = (stored ?? []).Where(allowed.Contains).Distinct(StringComparer.Ordinal).ToList();
    result.AddRange(defaults.Where(key => !result.Contains(key, StringComparer.Ordinal)));
    return result;
}
```

Add `NavigationOrder` and `NavigationDisplayMode` to `AppSettings`; build `NavigationItems` from the normalized order during settings load, expose the three display choices, and save after `MoveNavigationItem` or display-mode changes.

- [ ] **Step 4: Run focused tests and confirm pass**

Run the Step 2 command. Expected: PASS.

### Task 2: Shared Navigation, Segmented, and Compact Form Resources

**Files:**
- Modify: `src/HanabePhotoManager.App/Themes/Tokens/Icons.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Navigation.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Inputs.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Layout.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/DesignSystemResourceTests.cs`

**Interfaces:**
- Produces: geometry keys for every destination; `Navigation.ReorderableItem`, `Navigation.Segment`, `Navigation.SegmentItem`, `Input.SettingsComboBox`, and `Layout.SettingsGroup` resources.

- [ ] **Step 1: Add failing resource-key assertions**

```csharp
[Theory]
[InlineData("Icon.Import")]
[InlineData("Icon.Library")]
[InlineData("Icon.Map")]
[InlineData("Navigation.ReorderableItem")]
[InlineData("Input.SettingsComboBox")]
public void RequiredNavigationResourcesExist(string key) => FindResource(key).Should().NotBeNull();
```

- [ ] **Step 2: Run resource tests and confirm failure**

Run: `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter FullyQualifiedName~DesignSystemResourceTests`

Expected: FAIL for missing resource keys.

- [ ] **Step 3: Add 20px outline geometries and shared templates**

Use `Path` geometry with round caps, semantic brushes, 40px navigation rows, 36–40px inputs, and existing focus resources. The segmented control must use real `RadioButton` or `Button` controls with `AutomationProperties.Name`; do not use clickable `TextBlock` elements.

- [ ] **Step 4: Run resource tests and confirm pass**

Run the Step 2 command. Expected: PASS.

### Task 3: Reorderable Sidebar and Brand Image

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `NavigationItems`, `NavigationDisplayMode`, `MoveNavigationItem(sourceKey, targetKey)`, and icon resources from Tasks 1–2.

- [ ] **Step 1: Replace fixed business navigation buttons with one bound list**

```xml
<ListBox x:Name="PrimaryNavigationList"
         ItemsSource="{Binding NavigationItems}"
         AllowDrop="True"
         PreviewMouseLeftButtonDown="Navigation_PreviewMouseLeftButtonDown"
         PreviewMouseMove="Navigation_PreviewMouseMove"
         Drop="Navigation_Drop" />
```

The item template binds icon geometry and label visibility/alignment from display mode, keeps the destination command, and sets `ToolTip` plus `AutomationProperties.Name` to the label.

- [ ] **Step 2: Implement thresholded WPF drag/drop mechanics**

Capture source item and pointer origin on mouse-down, begin drag only after `SystemParameters.MinimumHorizontalDragDistance` or `MinimumVerticalDragDistance`, resolve the target container, and call `MoveNavigationItem`. Invalid/self drops do nothing; Escape clears pending drag state.

- [ ] **Step 3: Fix brand scaling and footer boundaries**

Set the brand `Image.Stretch` to `Uniform`, remove clipping/cropped fixed viewport behavior, and leave theme/settings in a non-reorderable footer.

- [ ] **Step 4: Build the App project**

Run: `dotnet build src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release /warnaserror`

Expected: build succeeds with zero warnings and zero XAML binding/compiler errors.

### Task 4: Settings Layout and Appearance Options

**Files:**
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml.cs`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Sidebar.xaml`

**Interfaces:**
- Consumes: `NavigationDisplayMode` choices and shared compact form resources.

- [ ] **Step 1: Correct settings navigation geometry**

Remove the `TabControl` top padding that creates the gap; make the left tab strip start below the page divider and stretch to the available bottom edge using the shell/sidebar surface brush rather than a page-local color.

- [ ] **Step 2: Add the three-state menu presentation setting**

Bind a shared segmented selector to `NavigationDisplayMode` with the labels `文字`, `图标`, and `图标和文字`.

- [ ] **Step 3: Rebuild Appearance as compact settings groups**

Group theme, navigation presentation, background/material, and application icon separately. Place visible labels above `Input.SettingsComboBox` controls with consistent 8px internal and 20–24px section spacing. Keep background source, display mode, glass slider, and background actions inside one bounded group.

- [ ] **Step 4: Build the App project**

Run the Task 3 Step 4 command. Expected: PASS.

### Task 5: Unified Browse Workspace and Connected Devices Height

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`

**Interfaces:**
- Preserves all existing browse and device bindings/commands.

- [ ] **Step 1: Keep calendar, People, and thumbnail-size controls unchanged**

Move no bindings or dimensions for these three areas beyond the parent grid changes needed to place the unified workspace beside them.

- [ ] **Step 2: Merge browse controls into one responsive panel**

Create a single `Card.Subtle` workspace with three rows: primary filters; manual category/custom tag actions; intelligent recognition. Use shared columns and `WrapPanel` fallbacks so labels, combo boxes, and action buttons align without tall nested vertical cards.

- [ ] **Step 3: Remove forced empty device action height**

Replace fixed/star rows that reserve the large blank action area with `Auto` rows and a content list constrained by the page scroll viewer. The selected-device area grows only when its actual file/folder overview is populated.

- [ ] **Step 4: Build the App project**

Run the Task 3 Step 4 command. Expected: PASS.

### Task 6: Map and Compression Control Consistency

**Files:**
- Modify: `src/HanabePhotoManager.App/Map/MapPage.xaml`
- Modify: `src/HanabePhotoManager.App/Map/MapPage.xaml.cs`
- Modify: `src/HanabePhotoManager.App/Compression/CompressionPage.xaml`

**Interfaces:**
- Preserves map view selection behavior and compression target bindings.

- [ ] **Step 1: Replace Map browser tabs with shared segmented selection**

Use two equal-height semantic controls styled with `Navigation.SegmentItem`. Bind or handle the selected mode and toggle the two existing content presenters; keep all list and assignment commands unchanged.

- [ ] **Step 2: Restyle the Compression target group**

Apply `Input.SettingsComboBox` to the target and unit selectors, shared TextBox styling to the numeric limit, and a two-row grid with the target selector spanning the width. Remove native white chrome and keep the explanatory text directly below.

- [ ] **Step 3: Build and run focused App tests**

Run: `dotnet build HanabePhotoManager.sln -c Release /warnaserror`

Then: `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --no-build`

Expected: both commands PASS.

### Task 7: Full Verification and Documentation

**Files:**
- Modify if shared rules changed: `docs/design-system.md`

- [ ] **Step 1: Run full automated verification**

Run: `dotnet build HanabePhotoManager.sln -c Release /warnaserror`

Run: `dotnet test HanabePhotoManager.sln -c Release --no-build`

Expected: zero build warnings/errors and all tests pass.

- [ ] **Step 2: Run the Windows UI smoke matrix**

Verify Light → Dark → Light, all three menu modes, icon-only tooltips and keyboard focus, drag reorder plus restart persistence, Settings edge alignment and compact background form, Browse alignment at minimum and large window widths, Map mode switching, Compression focus/dropdowns, device expansion, and uncropped brand artwork.

- [ ] **Step 3: Update the sole UI authority only if a reusable rule was added**

Document only enduring shared navigation/form behavior in `docs/design-system.md`; do not copy task status or screenshot-specific notes.

