# Coding Style

> **Purpose:** Define the authoritative implementation conventions for C#, WPF, XAML, ResourceDictionary, Style, and Theme code.  
> **Scope:** Production and test source under `src/` and `tests/`.  
> **Audience:** Contributors writing or reviewing code and XAML.  
> **References:** [architecture.md](architecture.md), [components.md](components.md), [design-system.md](design-system.md)

## Table of Contents

- [Repository Baseline](#repository-baseline)
- [C# and Naming](#c-and-naming)
- [MVVM and WPF](#mvvm-and-wpf)
- [XAML](#xaml)
- [ResourceDictionary](#resourcedictionary)
- [Styles and Themes](#styles-and-themes)
- [Tests](#tests)

## Repository Baseline

All projects inherit nullable reference types, implicit usings, C# 12, warnings-as-errors, and deterministic builds from `Directory.Build.props`. Do not disable these settings to make a change compile. The SDK is pinned by `global.json`. Match local formatting and avoid formatting unrelated files.

## C# and Naming

- Use file-scoped namespaces for new files.
- Prefer immutable records or `init` properties for values; use mutable classes for observable or stateful behavior.
- Validate public arguments at boundaries. Use explicit results for expected failure and exceptions for exceptional/external failure.
- Accept and propagate `CancellationToken` in long-running filesystem, network, model, and database work.
- Use `async`/`await`; never block with `.Result`, `.Wait()`, or synchronous dispatcher waits.
- Dispose streams, images, database objects, and native resources deterministically.
- Do not silently swallow exceptions in new code; convert them to an intentional fallback, result, or user-visible error.
- Never log or persist credentials, tokens, cookies, passwords, OTPs, QR content, or authorization headers.
- Public identifiers use PascalCase; locals and parameters use camelCase; private fields use `_camelCase`.
- Async methods end in `Async`; booleans read as predicates; commands end in `Command`.
- Namespaces follow `HanabePhotoManager.<Project>[.<Feature>]`.
- Types use established suffixes: `ViewModel`, `Page`, `Window`, `Service`, `Store`, `Policy`, `Planner`, or `Calculator`.
- Avoid vague names such as `Common`, `Helper`, `Utils`, and `Manager2`.

## MVVM and WPF

Use CommunityToolkit.Mvvm's `ObservableObject`, generated observable properties, and relay commands consistently with existing ViewModels. Notify dependent properties and command availability when source state changes.

Views own layout, bindings, and view mechanics. ViewModels own observable presentation state and commands. Services own filesystem, metadata, image, ML, cloud, and operating-system work. Code-behind is limited to WPF mechanics listed in [architecture.md](architecture.md); it must not own business decisions or persistence.

Update bound collections on the UI dispatcher. Keep IO, hashing, decoding, metadata extraction, and inference off the UI thread. Freeze shareable WPF Freezables produced off-thread.

## XAML

- Prefer bindings and commands to imperative assignment.
- Use `StaticResource` for invariant resources and `DynamicResource` only when runtime replacement is required.
- Make non-default binding modes explicit and define intentional fallback/null behavior.
- Reuse converter resources instead of duplicating converter implementations.
- Page resources may define page layout and feature data templates, but may not redefine shared control appearance.
- Preserve keyboard access, focus behavior, automation naming, and list virtualization.
- Use `x:Name` only for code-behind, element binding, focus, or automation access.

Visual values, states, density, and appearance are defined only by [design-system.md](design-system.md).

## ResourceDictionary

- One dictionary owns one category matching the current `Themes` directories.
- Merge dependencies in order: raw colors, semantic brushes, tokens/typography/motion, then controls.
- Light and Dark expose identical public keys with compatible types.
- Raw color literals stay in the raw color dictionaries identified by the design system.
- Shared controls consume semantic resources, never raw colors.
- Keys describe semantics, not literal values, screens, temporary trends, or theme names.
- Search all XAML and consult [resource-dictionary-structure.md](resource-dictionary-structure.md) before adding or removing keys.

## Styles and Themes

- Shared Styles and ControlTemplates live in the matching `Themes/Controls` dictionary.
- Use keyed styles for semantic variants and implicit styles only when every instance should inherit the behavior.
- Use `BasedOn` when structure and meaning match; do not copy templates.
- Keep state triggers in the component that owns them.
- Theme switching remains centralized in theme entry dictionaries and `ThemeManager`; Views and ViewModels do not branch on theme.
- A new theme must implement the same resource-key contract and composition mechanism as existing themes.

Component creation decisions belong to [components.md](components.md); UI semantics belong to [design-system.md](design-system.md).

## Tests

Tests follow the same nullable and warning rules. Use xUnit and FluentAssertions, keep temporary files in test-owned directories, and avoid dependence on a user's library, credentials, network account, locale, or app settings. Required test selection belongs to [testing.md](testing.md).
