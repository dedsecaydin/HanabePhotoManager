# Navigation Transitions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add smooth primary navigation mode/page transitions and settings secondary-navigation/content transitions.

**Architecture:** Keep navigation state in the existing view models and implement
visual transitions at the WPF view layer. Use cancellable code-behind animations
for ordered page changes and shared TabControl hooks for settings transitions.

**Tech Stack:** .NET 8, WPF, XAML storyboards, xUnit/FluentAssertions.

## Global Constraints

- Do not commit or push.
- Respect Windows reduced-animation settings.
- Rapid input must settle on the latest requested state.

---

### Task 1: Primary navigation transitions

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml.cs`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

- [ ] Add a failing structural test for three-mode cross-fades and cancellable page transitions.
- [ ] Route primary page clicks through an ordered fade-out/navigation/fade-in method.
- [ ] Animate icon and label opacity plus horizontal translation using the old and new modes.
- [ ] Run the focused tests.

### Task 2: Settings secondary transitions

**Files:**
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml.cs`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Navigation.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

- [ ] Add a failing structural test for settings selection and content transitions.
- [ ] Name the settings TabControl and selected-content host.
- [ ] Animate selected secondary navigation and the complete content host.
- [ ] Run the focused tests.

### Task 3: Verification and launch

- [ ] Run `dotnet test -clp:Summary`.
- [ ] Build Release with zero warnings and errors.
- [ ] Restart the Release application and inspect Git status.
