# AI-native Desktop UI Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a token-driven Light/Dark WPF component system and migrate every UI module without changing business behavior.

**Architecture:** `App.xaml` loads one theme entry dictionary. Theme dictionaries merge semantic colors, shared tokens, typography, motion, and component dictionaries; pages consume only semantic resources and shared styles. Migration follows Foundation → Button → Input → Card → Dialog → Sidebar → Navigation → MainWindow → PhotoViewer → Cleanup, with a Release build after every task.

**Tech Stack:** .NET 8, WPF XAML, C#, xUnit, FluentAssertions.

## Global Constraints

- Do not change ViewModel behavior, bindings, commands, APIs, services, or data models.
- UI font stack is `Segoe UI Variable, Microsoft YaHei UI`; no other general UI font stacks.
- Raw colors are allowed only in `Themes/Colors/Colors.Light.xaml` and `Colors.Dark.xaml`.
- Do not proceed to the next task unless the current task builds successfully.
- Use isolated output: `.artifacts/ui-refactor-verification`.
- Preserve the immersive dark photo canvas as a documented semantic exception.
- Current export has no `.git`; omit commit steps until work is moved to a Git worktree.

---

### Task 1: Foundation

**Files:**
- Create: `src/HanabePhotoManager.App/Themes/Tokens/{Spacing,Radius,Sizing,Shadows,Icons}.xaml`
- Create: `src/HanabePhotoManager.App/Themes/Colors/{Colors.Light,Colors.Dark,Brushes.Light,Brushes.Dark}.xaml`
- Create: `src/HanabePhotoManager.App/Themes/Typography/{FontFamilies,TypeScale}.xaml`
- Create: `src/HanabePhotoManager.App/Themes/Motion/{Durations,Easings}.xaml`
- Create: `src/HanabePhotoManager.App/Themes/Themes/{Light,Dark}.xaml`
- Modify: `src/HanabePhotoManager.App/App.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Produces:** Stable semantic resource keys documented in `docs/resource-dictionary-structure.md`.

- [ ] Add tests that load every dictionary, assert Light/Dark key parity, assert the required font stack, and reject raw colors outside `Themes/Colors/Colors.*.xaml`.
- [ ] Run `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --artifacts-path .artifacts/ui-refactor-verification --filter ControlThemeTests`; expect the new tests to fail before dictionaries exist.
- [ ] Create the dictionaries and make `App.xaml` merge `Themes/Themes/Light.xaml`; do not migrate controls yet.
- [ ] Run the filtered tests; expect all ControlThemeTests to pass after updating obsolete Glass-specific assertions to semantic-token assertions.
- [ ] Run `dotnet build HanabePhotoManager.sln -c Release --artifacts-path .artifacts/ui-refactor-verification`; require exit code 0.

### Task 2: Button Library

**Files:**
- Create: `src/HanabePhotoManager.App/Themes/Controls/Buttons.xaml`
- Modify: `src/HanabePhotoManager.App/App.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Produces:** `Button.Primary`, `Button.Secondary`, `Button.Ghost`, `Button.Danger`, `Button.Icon`, `Button.Toolbar`, `Button.Disclosure`.

- [ ] Add resource tests for all seven keys and tests that each template includes Hover, Pressed, Focus and Disabled triggers using semantic brushes.
- [ ] Run the filtered test and confirm failure for missing Button resources.
- [ ] Implement the base Button template and `BasedOn` variants; remove scale animation from the global template but keep old keys as temporary aliases.
- [ ] Run filtered tests and the Release build; require both to pass.

### Task 3: Input Library

**Files:**
- Create: `src/HanabePhotoManager.App/Themes/Controls/Inputs.xaml`
- Create: `src/HanabePhotoManager.App/Themes/Controls/Selection.xaml`
- Create: `src/HanabePhotoManager.App/Themes/Controls/ScrollBars.xaml`
- Modify: `src/HanabePhotoManager.App/App.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Produces:** Unified TextBox, PasswordBox, ComboBox, CheckBox, RadioButton, Slider and ScrollBar.

- [ ] Add tests for 36/40px sizing and Normal/Hover/Focus/Disabled/Validation states; keep PART names required by WPF.
- [ ] Run tests and confirm failure before implementation.
- [ ] Move and neutralize existing input templates, replacing all Glass resource names with semantic keys.
- [ ] Run filtered tests and Release build; require both to pass.

### Task 4: Card and Layout Library

**Files:**
- Create: `src/HanabePhotoManager.App/Themes/Controls/Cards.xaml`
- Create: `src/HanabePhotoManager.App/Themes/Controls/Layout.xaml`
- Modify: `src/HanabePhotoManager.App/App.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Produces:** Card variants, Page/Section headers, page padding, max-width and grid gutter resources.

- [ ] Add tests for Card and Layout keys and assert Card uses Radius.Card, standardized padding and no default shadow.
- [ ] Run tests to observe missing-resource failures.
- [ ] Implement Card/Surface/Layout resources; retain GlassPanel/SoftCard/SidebarCard only as temporary aliases.
- [ ] Run filtered tests and Release build; require both to pass.

### Task 5: Dialogs

**Files:**
- Create: `src/HanabePhotoManager.App/Themes/Controls/Dialogs.xaml`
- Modify: `src/HanabePhotoManager.App/DeleteConfirmationWindow.xaml`
- Modify: `src/HanabePhotoManager.App/RemarkPromptWindow.xaml`
- Modify: `src/HanabePhotoManager.App/Contest/ContestPickerWindow.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`
- Preserve: all three `.xaml.cs` files.

**Produces:** Shared dialog window surface, title/body/actions and keyboard focus behavior.

- [ ] Add static XAML tests proving all three dialogs use Dialog resources and contain no raw colors or local implicit Button styles.
- [ ] Run tests and confirm failure on existing dialog markup.
- [ ] Migrate one dialog at a time; after each file run the Release build before touching the next.
- [ ] Run filtered tests and full App test project; require exit code 0.

### Task 6: Sidebar

**Files:**
- Create: `src/HanabePhotoManager.App/Themes/Controls/Sidebar.xaml`
- Modify: only the sidebar region and resources in `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Produces:** 232px Sidebar container, header and footer; no navigation-item migration yet.

- [ ] Replace obsolete fixed-icon tests with sidebar width, token usage, focus and no-raw-color assertions.
- [ ] Run tests to confirm current markup fails the new contract.
- [ ] Migrate the sidebar shell without changing commands or visibility bindings.
- [ ] Run navigation ViewModel tests, ControlThemeTests and Release build; require all to pass.

### Task 7: Navigation

**Files:**
- Create: `src/HanabePhotoManager.App/Themes/Controls/Navigation.xaml`
- Create/extend: `src/HanabePhotoManager.App/Themes/Tokens/Icons.xaml`
- Modify: only navigation items in `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Produces:** Navigation.Item, Segment.Item, Tab.Item and line-icon geometries.

- [ ] Add tests that reject Emoji navigation content and require Geometry icons, selection indicator, keyboard focus and existing command bindings.
- [ ] Run tests and confirm failure on current text/Emoji icons.
- [ ] Replace icons and migrate Win11NavButton, PreviewSegmentButton and MapModeTabItem consumers without changing commands.
- [ ] Run navigation tests, ControlThemeTests and Release build; require all to pass.

### Task 8: MainWindow

**Files:**
- Create: `src/HanabePhotoManager.App/Themes/Controls/{Toolbars,Lists,Menus,Status}.xaml`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify sequentially: `src/HanabePhotoManager.App/Cloud/CloudPage.xaml`
- Modify sequentially: `src/HanabePhotoManager.App/Compression/CompressionPage.xaml`
- Modify sequentially: `src/HanabePhotoManager.App/Watermark/WatermarkPage.xaml`
- Modify sequentially: `src/HanabePhotoManager.App/Contest/ContestOpenPage.xaml`
- Modify sequentially: `src/HanabePhotoManager.App/Contest/ContestJudgedPage.xaml`
- Modify sequentially: `src/HanabePhotoManager.App/Map/MapPage.xaml`
- Preserve: `src/HanabePhotoManager.App/MainWindow.xaml.cs` and `ViewModels/MainWindowViewModel.cs` unless theme switching requires isolated UI glue.

**Produces:** Unified main workspace, toolbars, lists and status states.

- [ ] Snapshot all Binding, Command and event-handler attribute values before modification with a test helper; make the test compare the post-migration set.
- [ ] Migrate MainWindow in separate build gates: header/toolbars; home/import; gallery/preview; face/map/cloud hosts; settings.
- [ ] After each region run Release build and the relevant App tests; stop on failure.
- [ ] Migrate each hosted functional page as its own submodule in this order: Cloud, Compression, Watermark, Contest Open, Contest Judged, Map; run the Release build after every single page.
- [ ] Remove duplicate local component styles only after their final consumer is migrated.
- [ ] Run the full solution test suite and Release build; require exit code 0.

### Task 9: PhotoViewer

**Files:**
- Modify: `src/HanabePhotoManager.App/PhotoViewerWindow.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`
- Preserve: `PhotoViewerWindow.xaml.cs`, `PhotoViewerViewModel.cs`.

**Produces:** Tokenized immersive viewer with shared toolbar and controls.

- [ ] Add tests requiring the approved `Brush.Viewer.Canvas` exception and shared Button/Toolbar styles while rejecting other raw colors.
- [ ] Run tests and confirm failure on current markup.
- [ ] Migrate viewer chrome; keep image canvas dark in both themes.
- [ ] Run PhotoViewer tests, ControlThemeTests and Release build; require all to pass.

### Task 10: Cleanup and Final Documentation

**Files:**
- Modify: any XAML file still referencing temporary aliases, one file at a time.
- Modify: `src/HanabePhotoManager.App/App.xaml`
- Create: `docs/design-system.md`
- Deprecate: `design-system/hanabe-photo-manager/MASTER.md`

**Produces:** One final Design System and zero unapproved visual literals.

- [ ] Add a repository XAML audit test for forbidden raw colors, font families, unapproved radius/spacing values, local implicit shared-control styles and obsolete Glass/Win11 keys.
- [ ] Replace remaining temporary aliases one file at a time and run the Release build after each file.
- [ ] Remove temporary aliases only after repository search reports zero consumers.
- [ ] Generate `docs/design-system.md` from implemented keys, components, states, layouts, exceptions and contribution rules; mark the former MASTER as superseded.
- [ ] Run `dotnet test HanabePhotoManager.sln -c Release --artifacts-path .artifacts/ui-refactor-verification`; require zero failures.
- [ ] Run `dotnet publish src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release -r win-x64 --self-contained false --artifacts-path .artifacts/ui-refactor-verification`; require exit code 0.
- [ ] Launch the isolated publish and manually verify Light/Dark, keyboard traversal, scaling, Loading/Empty/Error, all navigation targets and the photo viewer.
