# Component Governance

> **Purpose:** Define when to reuse, extend, or create reusable UI and application components.  
> **Scope:** Shared WPF styles, templates, controls, pages, dialogs, ViewModels, and services in `HanabePhotoManager.App`.  
> **Audience:** Contributors adding or changing reusable application building blocks.  
> **References:** [`architecture.md`](architecture.md), [`design-system.md`](design-system.md), [`component-inventory.md`](component-inventory.md)

## Table of Contents

- [What Counts as a Component](#what-counts-as-a-component)
- [Required Decision Order](#required-decision-order)
- [Reuse, Extend, or Create](#reuse-extend-or-create)
- [Naming](#naming)
- [Placement and Ownership](#placement-and-ownership)
- [Prohibited Duplication](#prohibited-duplication)
- [Component Change Checklist](#component-change-checklist)

## What Counts as a Component

A component is a reusable unit with a stable responsibility and public usage contract. In this project it may be a keyed WPF Style or ControlTemplate, a themed ResourceDictionary group, a page or dialog pattern, a focused ViewModel, a service interface/implementation pair, or a deterministic policy used by multiple workflows.

A repeated visual value is not automatically a component; visual semantics and tokens are owned by [`design-system.md`](design-system.md). A one-off page layout is not shared merely because it uses common controls.

## Required Decision Order

Before adding a component:

1. Search `src/HanabePhotoManager.App/Themes/Controls` and [`component-inventory.md`](component-inventory.md).
2. Search production code for the same responsibility, not only the proposed name.
3. Check whether an existing component supports the needed semantic role.
4. Prefer composition and existing properties.
5. Extend an existing component only when the new behavior preserves its meaning for current consumers.
6. Create a new component only when the responsibility is distinct and has a realistic second consumer or needs isolated testing.

Record this reuse decision in the feature analysis before implementation.

## Reuse, Extend, or Create

### Reuse

Reuse when structure, behavior, and semantic role already match. Page-local spacing or content differences should be supplied by layout and content, not by copying a template. Use existing services when their contract already expresses the operation and lifetime required.

### Extend

Extend when the base semantics remain unchanged and existing consumers are not forced into page-specific behavior. For WPF styles, prefer `BasedOn` and a named variant in the owning Controls dictionary. For services, add a cohesive operation only if it belongs to the same external capability and can be tested without unrelated setup.

Do not extend a general component with flags whose only purpose is one screen. That is a separate composed component or page-local layout concern.

### Create

Create when at least one condition holds:

- The unit has a different semantic role from all existing components.
- Repetition has already appeared in two locations and a shared contract is stable.
- Isolation is necessary for deterministic testing or to preserve dependency direction.
- A complex interaction needs one owner instead of duplicated event handling.

A new shared UI component requires an owning ResourceDictionary, Light/Dark verification, relevant resource tests, and an update to `component-inventory.md`. Its visual design must already be allowed by [`design-system.md`](design-system.md); otherwise update that authority first.

## Naming

- C# types use responsibility names: `PhotoLocationService`, `MapPhotosViewModel`, `ImageCompressionPlanner`.
- Interfaces describe capability (`ICloudProvider`, `IFileHasher`), not implementation technology unless technology is the capability.
- Pages end in `Page`, windows/dialog hosts in `Window`, presentation state in `ViewModel`, external operations in `Service`, and deterministic decisions in `Policy`, `Planner`, or `Calculator`.
- Shared Style keys use `Category.Role` and established vocabulary, such as `Button.Primary`, `Card.Default`, or `Layout.PageSurface`.
- Resource keys must describe semantics rather than a page, temporary design trend, or literal value.
- Avoid `Common`, `Helper`, `Utils`, `BaseManager`, and numbered variants; choose the actual responsibility.

UI role names and the component catalog itself are defined only by [`design-system.md`](design-system.md).

## Placement and Ownership

- Shared visual components belong in the matching `Themes/Controls/*.xaml` dictionary.
- Page-only layout stays with the page and must not redefine shared control appearance.
- General ViewModels live under `App/ViewModels`; feature-contained ViewModels may live beside their page when that feature already owns a directory, as Cloud and Watermark do.
- Portable rules belong in Core; external implementations in Infrastructure; WPF/Windows-specific components in App. See [`architecture.md`](architecture.md).
- Tests follow the production owner and use the corresponding test project.

## Prohibited Duplication

- Do not copy an existing ControlTemplate into a page, dialog, or feature dictionary.
- Do not create a second Style key for the same semantic role.
- Do not reproduce shared colors, tokens, typography, or component-state rules outside `design-system.md` and their resource implementation.
- Do not introduce parallel services that read or write the same persistent data without a documented ownership split.
- Do not duplicate domain decisions in ViewModels and services.
- Do not use a page name to disguise a duplicate shared component.

## Component Change Checklist

- Reuse search and decision are recorded.
- Ownership and dependency layer match `architecture.md`.
- Naming follows this document.
- Existing consumers remain compatible or are migrated together.
- Tests cover the reusable behavior at the owning layer.
- UI work links to, but does not restate, `design-system.md`.
- Shared resource additions update `component-inventory.md`.
- Required build and tests must pass before completion.
