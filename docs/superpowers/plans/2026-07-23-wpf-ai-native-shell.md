# Hanabe Photo Manager AI-native App Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. The user explicitly prohibited additional review agents. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the project-specific `wpf-ai-native-shell` Skill and reshape MainWindow into a continuous AI-native desktop shell without changing bindings, commands, events, ViewModels, APIs, data, or business flow.

**Architecture:** Keep the existing WPF visual tree and navigation behavior, but move shell appearance into semantic Light/Dark brushes, shared effects, and Shell/Sidebar/Layout styles. Flatten only the outer workspace and home section wrappers; retain semantic thumbnail, device, and folder items.

**Tech Stack:** .NET 8, C# 12, WPF XAML, ResourceDictionary, xUnit, PowerShell, Windows UI Automation.

## Global Constraints

- Modify only the Skill, `docs/design-system.md`, directly related theme/token/shared visual resources, and `MainWindow.xaml`.
- Preserve every existing Binding, Command, event-handler attribute, API, data structure, ViewModel, and business flow.
- Preserve the soft, light, low-saturation, neutral palette and equivalent Light/Dark resource keys.
- Use no page-level raw colors, arbitrary effects, gradients, radii, shadows, blur values, or spacing.
- Save eight Visual QA screenshots to a new directory without overwriting existing files.
- Do not create commits because `D:\APP` is not a Git repository.

---

### Task 1: Create and validate the project Skill

**Files:**
- Create: `.codex/skills/wpf-ai-native-shell/SKILL.md`
- Create: `.codex/skills/wpf-ai-native-shell/agents/openai.yaml`

**Interfaces:**
- Consumes: `AGENTS.md`, `docs/design-system.md`, `docs/components.md`, `docs/coding-style.md`.
- Produces: explicit trigger `$wpf-ai-native-shell` and automatic matching metadata for Hanabe MainWindow/App Shell work.

- [ ] Initialize the Skill using `skill-creator/scripts/init_skill.py` at `.codex/skills` with UI metadata.
- [ ] Replace the generated instructions with the project-root gate, exact scope, resource rules, restrained effects hierarchy, minimal-change workflow, build/test/publish commands, and Visual QA checklist from the approved design.
- [ ] Run `skill-creator/scripts/quick_validate.py .codex/skills/wpf-ai-native-shell` and require exit code 0.

### Task 2: Update the design authority and semantic resources

**Files:**
- Modify: `docs/design-system.md`
- Modify: `src/HanabePhotoManager.App/Themes/Colors/Colors.Light.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Colors/Colors.Dark.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Colors/Brushes.Light.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Colors/Brushes.Dark.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Tokens/Shadows.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Layout.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Sidebar.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Navigation.xaml`
- Modify if the public resource inventory changes: `docs/resource-dictionary-structure.md`, `docs/component-inventory.md`

**Interfaces:**
- Produces: symmetric semantic resources for shell background/material/highlight and three effect levels; shared `Layout.Shell`, `Layout.Workspace`, `Layout.TopBar`, `Layout.HomeSection`, and refined Sidebar/navigation styles.

- [ ] Replace the blanket visual-effects ban in `design-system.md` with explicit allowed locations, contrast/performance rules, and token-only enforcement.
- [ ] Add matching Light/Dark raw colors and semantic brushes only where existing keys cannot express the approved shell hierarchy.
- [ ] Add no-shadow/light floating/dialog-or-emphasis effect levels in `Shadows.xaml`; keep ordinary content shadow-free.
- [ ] Add shared shell/layout styles and refine Sidebar/navigation states without page-local ControlTemplate duplication.
- [ ] Scan both theme entries to prove every new public resource key resolves in Light and Dark.

### Task 3: Reshape MainWindow and the home page

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`

**Interfaces:**
- Consumes: Task 2 shell/layout/sidebar semantic styles.
- Preserves: all existing `{Binding ...}`, `Command=`, and event-handler attribute values.

- [ ] Capture pre-edit Binding, Command, and event-handler inventories from `MainWindow.xaml` into temporary verification output.
- [ ] Replace the outer workspace `Card.Default` with the continuous workspace style, remove the Sidebar/content gap, and apply the shared top-bar divider.
- [ ] Convert the three home statistic cards into one aligned summary strip with column separators.
- [ ] Remove outer Card wrappers from Recent Photos and device/folder sections; apply section spacing/dividers while retaining thumbnail, device, and folder item surfaces.
- [ ] Remove the page-local shell gradient/effect declarations superseded by shared resources; do not touch other page internals.
- [ ] Compare post-edit Binding, Command, and event-handler inventories byte-for-byte with the captured inventories.

### Task 4: Automated verification and publish

**Files:**
- Output only: build/test/publish artifacts under existing project conventions.

- [ ] Run `dotnet build HanabePhotoManager.sln -c Release /warnaserror` and stop on any failure.
- [ ] Run `dotnet test HanabePhotoManager.sln -c Release --no-build` and record total pass/fail/skip counts.
- [ ] Inspect `tools/Publish-Clean.ps1`; use it when it is the formal win-x64 path, otherwise run `dotnet publish src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release -r win-x64`.
- [ ] Confirm the published executable and required resources exist.

### Task 5: Visual QA and screenshots

**Files:**
- Create: `artifacts/visual-qa/2026-07-23-ai-native-shell-<timestamp>/`

- [ ] Launch the freshly published executable and create a new screenshot directory.
- [ ] Capture 首页、导入、照片图库、人物查找、地图照片、批量压缩、批量水印、设置 as distinct PNG files.
- [ ] Inspect screenshots for continuous shell, dashboard-card residue, effect restraint, contrast, clipping, overflow, and alignment.
- [ ] Exercise Light/Dark, a smaller supported window, keyboard focus, Hover/Pressed/Disabled where reachable, and available Loading/Empty/Error states; record anything not deterministically reachable.
- [ ] If the home view still reads as card collage or effects are excessive, adjust only Tasks 2-3 files and repeat build plus screenshots.

## Self-review

- Spec coverage: all approved Skill, documentation, Shell, Sidebar, top bar, home, validation, publish, and eight-screenshot requirements map to Tasks 1-5.
- Scope: no ViewModel, C#, API, data, binding, command, event, or non-shell page information-architecture work is planned.
- Environment: no commit step exists because the confirmed workspace has no `.git`.
