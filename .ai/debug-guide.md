# AI Debug Guide

> **Purpose:** Provide evidence-first diagnostic paths for common project failures.  
> **Scope:** Theme, Binding, ResourceDictionary, MVVM, build-output, WebView2, and persistence symptoms.  
> **Audience:** AI agents diagnosing defects before proposing a fix.  
> **References:** [architecture.md](../docs/architecture.md), [coding-style.md](../docs/coding-style.md), [testing.md](../docs/testing.md)

## Table of Contents

- [Diagnostic Method](#diagnostic-method)
- [Theme](#theme)
- [Binding](#binding)
- [ResourceDictionary](#resourcedictionary)
- [MVVM](#mvvm)
- [Build Output Locked](#build-output-locked)
- [Map and WebView2](#map-and-webview2)
- [Persistence and Files](#persistence-and-files)

## Diagnostic Method

Reproduce with the smallest safe fixture, capture the exact symptom and first relevant exception, identify the owning layer, compare with a working sibling, and run the narrowest existing test. Diagnose before editing; fixes follow [workflow.md](../docs/workflow.md).

## Theme

1. Determine whether failure occurs at startup, only after switching, or only in one theme.
2. Check `App.xaml`, active theme entry, and `ThemeManager` replacement/persistence path.
3. Compare Light/Dark key names and types.
4. Trace the missing key backward through Controls → tokens/brushes → raw colors and verify merge order.
5. Search for page-local literals or theme branches.
6. Run `ThemeManagerTests`, `ControlThemeTests`, and `DesignSystemResourceTests`, then smoke Light → Dark → Light.

Implementation rules are in [coding-style.md](../docs/coding-style.md); visual correctness is defined only by [design-system.md](../docs/design-system.md).

## Binding

1. Capture the full WPF binding error: target, path, source type, and DataContext.
2. Confirm the View/ViewModel composition and DataContext lifetime.
3. Verify property spelling, visibility, converter, mode, and null/fallback behavior.
4. For stale values, verify property-change notification for the source and dependent properties.
5. For stale commands, verify CanExecute refresh.
6. For collections/cross-thread errors, verify dispatcher ownership and cancellation race handling.

Do not hide a real binding defect with broad fallback values or code-behind assignment.

## ResourceDictionary

1. Search the exact key in declarations and consumers.
2. Confirm the dictionary is merged before its consumer and that the URI/casing is correct.
3. Check duplicate keys and expected resource type.
4. Check Light/Dark parity and whether `StaticResource` versus `DynamicResource` matches runtime replacement.
5. Run resource/theme tests and build the full App project.

Use [resource-dictionary-structure.md](../docs/resource-dictionary-structure.md) as inventory, not as a competing rule source.

## MVVM

1. Classify the symptom as state, command, service, or view-mechanics failure.
2. Test the ViewModel/service without the View when possible.
3. Trace command input → state validation → service call → cancellation/progress → observable result.
4. Check generated observable partial methods, dependent notifications, and command enablement.
5. Check disposal/unsubscription for lifecycle leaks.
6. Keep the correction in the owning unit; do not move business logic into code-behind.

## Build Output Locked

If MSBuild reports DLL/EXE access or copy retries, identify the process holding output. Do not kill unrelated processes or delete broad directories. Prefer the isolated commands in [testing.md](../docs/testing.md) with one shared `--artifacts-path` for build and test.

## Map and WebView2

Separate WPF host initialization, WebView2 runtime/user-data, bundled Leaflet asset loading, JavaScript bridge messages, and map-data preparation. Check asset copy paths and browser console/devtools errors. Do not use network tiles or provider APIs as a substitute for diagnosing bundled asset/bridge failures.

## Persistence and Files

Use disposable directories. Trace path resolution, directory initialization, hash/transfer, journal/store serialization, atomicity, cancellation, and restart behavior. For cloud state, distinguish queue, cache, SQLite index, encrypted session, and provider failures. Never print secrets or test against a user's real library.
