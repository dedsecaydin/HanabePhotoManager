# Onboarding Replay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a settings entry that immediately replays a three-step onboarding tutorial.

**Architecture:** Extend `MainWindowViewModel` with explicit onboarding step state and commands, reuse the existing persisted completion flag, and bind a richer overlay plus a settings replay button to those commands.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm, xUnit, FluentAssertions.

## Global Constraints

- Preserve the existing `settings.json` schema and reuse `HasCompletedOnboarding`.
- Do not commit or push.
- Tutorial replay must begin immediately and must not make the tutorial appear on every later startup.

---

### Task 1: Onboarding state and commands

**Files:**
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Test: `tests/HanabePhotoManager.App.Tests/AppSettingsStoreTests.cs`

**Interfaces:**
- Produces: `ReplayOnboardingCommand`, `PreviousOnboardingStepCommand`, `NextOnboardingStepCommand`, `OnboardingStep`, `OnboardingTitle`, `OnboardingDescription`, `IsFirstOnboardingStep`, `IsLastOnboardingStep`.

- [ ] Add failing tests proving replay starts at step one and next/previous stay within the three-step range.
- [ ] Run the focused tests and verify they fail because the replay API is absent.
- [ ] Implement the properties and commands, raising dependent property notifications whenever the step changes.
- [ ] Reuse `DismissOnboardingAsync` for skip/finish and persist `HasCompletedOnboarding = true`.
- [ ] Run focused tests and verify they pass.

### Task 2: Settings entry and tutorial overlay

**Files:**
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Consumes: onboarding properties and commands from Task 1.

- [ ] Add failing source tests for the “再次体验新手教程” button and navigation controls.
- [ ] Run the focused tests and verify the required bindings are absent.
- [ ] Add the settings card under “常规”.
- [ ] Replace the static welcome overlay content with bound title, description, step indicator, previous, next/finish and skip controls.
- [ ] Run focused tests and verify they pass.

### Task 3: Verification and launch

**Files:**
- No additional production files.

- [ ] Run `dotnet test -clp:Summary --no-restore`.
- [ ] Stop the existing app process and run `dotnet build -c Release --no-restore -clp:Summary`.
- [ ] Launch the Release executable and verify the main window process is alive.
- [ ] Report test totals and Git status without committing or pushing.
