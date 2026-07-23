# Visual Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the sidebar, Appearance, Compression, and Browse visual language exactly as approved in the Visual Closure Addendum.

**Architecture:** Keep business state and commands unchanged. Make shell/page composition edits in `MainWindow.xaml` and `SettingsCenterPage.xaml`, and put reusable themed input behavior in existing control dictionaries. Validate both XAML structure and real WPF startup because static resource failures can compile successfully.

**Tech Stack:** .NET 8, C# 12, WPF/XAML, xUnit, FluentAssertions.

## Global Constraints

- Calendar and thumbnail-size behavior remain unchanged.
- Existing persisted tags remain available; only the Browse tag-creation entry point is removed.
- Use semantic resources and shared styles; no raw colors or third-party UI dependencies.
- The application must create a real visible main window after the final build.

---

### Task 1: Sidebar Footer and Brand Closure

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Tokens/Icons.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Consumes: existing theme toggle click handler and `ShowSettingsCommand`.
- Produces: icon-and-label footer controls plus square centered artwork-only brand header.

- [ ] **Step 1: Add failing XAML assertions**

```csharp
mainXaml.Should().Contain("AutomationProperties.Name=\"切换主题\"");
mainXaml.Should().Contain("AutomationProperties.Name=\"设置\"");
mainXaml.Should().NotContain("Text=\"Hanabe Photos\"");
mainXaml.Should().Contain("Stretch=\"Uniform\"");
```

- [ ] **Step 2: Run the focused test and confirm failure**

Run: `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter FullyQualifiedName~ControlThemeTests --artifacts-path .artifacts/visual-closure-red`

Expected: FAIL on missing footer icon/name or remaining brand text.

- [ ] **Step 3: Implement footer icons and artwork-only header**

Use the existing `Icon.Settings` geometry and add a theme sun/moon geometry if needed. Keep visible footer labels, because the user's “不用字” instruction applies to the brand header. Make the brand image square, centered, `Stretch="Uniform"`, and remove the `Hanabe Photos` TextBlock.

- [ ] **Step 4: Run the focused test and confirm pass**

Run the Step 2 command. Expected: PASS.

### Task 2: Themed Appearance and Compression Inputs

**Files:**
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Modify: `src/HanabePhotoManager.App/Compression/CompressionPage.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Inputs.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/DesignSystemResourceTests.cs`

**Interfaces:**
- Consumes: `BackgroundModes`, `BackgroundImageLayouts`, compression target bindings, and existing commands.
- Produces: themed compact controls without native white selection chrome.

- [ ] **Step 1: Add failing resource and XAML assertions**

```csharp
FindResource("Input.SettingsComboBox").Should().NotBeNull();
settingsXaml.Should().Contain("Style=\"{StaticResource Input.SettingsComboBox}\"");
compressionXaml.Should().Contain("Style=\"{DynamicResource Input.SettingsComboBox}\"");
```

- [ ] **Step 2: Run resource tests and confirm the new appearance assertion fails**

Run: `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter FullyQualifiedName~DesignSystemResourceTests --artifacts-path .artifacts/visual-closure-inputs`

Expected: FAIL until every target selector uses the shared template.

- [ ] **Step 3: Apply the shared template to all highlighted selectors**

Ensure `Input.SettingsComboBox` supplies its own WPF `ControlTemplate`, popup, item foreground/background, arrow, hover, focus, and disabled states. Apply it to both Appearance selectors and both Compression selectors; keep the compression mode selector stretched.

- [ ] **Step 4: Run resource tests and App Release build**

Run Step 2, then `dotnet build src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release /warnaserror --artifacts-path .artifacts/visual-closure-build`.

Expected: tests and build PASS with zero warnings.

### Task 3: Browse Workspace Consolidation

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Preserves: category, search, retouch, sort, rating, manual category, existing custom tag, and recognition bindings.
- Removes from the view only: `NewCustomTagName` and `CreateCustomTagCommand` controls.

- [ ] **Step 1: Add failing structure assertions**

```csharp
mainXaml.Should().NotContain("Text=\"调整\"");
mainXaml.Should().NotContain("NewCustomTagName");
mainXaml.Should().NotContain("Command=\"{Binding CreateCustomTagCommand}\"");
mainXaml.Should().NotContain("BorderThickness=\"1,0,1,0\"");
mainXaml.Should().Contain("x:Name=\"BrowseUnifiedWorkspace\"");
```

- [ ] **Step 2: Run the focused test and confirm failure**

Run the Task 1 Step 2 command. Expected: FAIL on the old label, creation controls, dividers, or missing unified workspace.

- [ ] **Step 3: Recompose the Browse workspace**

Place category chips, current-scope summary, filters, manual actions, and recognition inside one named panel. Give search the flexible `*` column with a larger minimum width. Remove the tag-name TextBox/Create Button and internal horizontal borders. Retain disclosure and Reset, but remove the `Adjust` TextBlock.

- [ ] **Step 4: Run full verification and visible startup smoke test**

Run:

```powershell
dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts/final-verification
dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts/final-verification
```

Expected: zero warnings/errors and all tests pass. Then launch the built executable and verify that `Hanabe Photo Manager Alpha` appears as a real window; inspect Appearance, Compression, and Browse in Dark and Light themes.

