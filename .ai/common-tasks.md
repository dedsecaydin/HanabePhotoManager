# AI Common Tasks

> **Purpose:** Provide project-specific execution sequences for recurring changes.  
> **Scope:** Pages, dialogs, themes, toolbars, and business features.  
> **Audience:** AI agents performing routine repository work.  
> **References:** [workflow.md](../docs/workflow.md), [components.md](../docs/components.md), [testing.md](../docs/testing.md), [design-system.md](../docs/design-system.md)

## Table of Contents

- [Add a Page](#add-a-page)
- [Add a Dialog](#add-a-dialog)
- [Add a Theme](#add-a-theme)
- [Add or Change a Toolbar](#add-or-change-a-toolbar)
- [Add a Business Feature](#add-a-business-feature)

These are procedures, not new rules. Follow the linked standards when a step requires a decision.

## Add a Page

1. Identify the feature owner using [architecture.md](../docs/architecture.md); inspect an adjacent Page/ViewModel and navigation tests.
2. Search shared controls and [component-inventory.md](../docs/component-inventory.md); record reuse/extension decisions.
3. Create focused ViewModel state/commands and services before binding the view.
4. Add the Page in the feature folder or established App location and wire navigation/composition.
5. Use only UI roles defined by [design-system.md](../docs/design-system.md); keep shared appearance out of page resources.
6. Add ViewModel/navigation/resource tests, build Release, run App tests, and smoke navigation/theme/keyboard behavior per [testing.md](../docs/testing.md).

## Add a Dialog

1. Confirm a dialog is necessary and inspect existing confirmation, remark, and picker windows.
2. Reuse shared dialog and button resources; do not copy a Window template.
3. Keep business decisions in the invoking ViewModel/service. Code-behind may adapt owner, focus, close, and result mechanics.
4. Define cancel, default action, validation, Escape, Enter, and owner/modal behavior.
5. Add logic/resource tests and smoke both themes, keyboard paths, long content, and display scaling.

## Add a Theme

1. Read [design-system.md](../docs/design-system.md) and the current [resource dictionary structure](../docs/resource-dictionary-structure.md).
2. Implement a new theme entry with the same public key/type contract and merge order described by [coding-style.md](../docs/coding-style.md).
3. Integrate it through the existing `ThemeManager` and preference mechanism; never add theme branches to Views/ViewModels.
4. Extend theme/resource tests and run the full App test set.
5. Smoke runtime switching across primary pages, dialogs, viewer, map shell, and disabled/focus/error states.

## Add or Change a Toolbar

1. Determine whether the toolbar is global or belongs to one workflow.
2. Reuse the established toolbar container and action components. Extend only through [components.md](../docs/components.md).
3. Bind actions to commands and expose tooltips/automation names for icon-only actions.
4. Preserve overflow, keyboard access, enabled state, and cancellation behavior.
5. Add command/resource tests and smoke at narrow and normal window sizes.

## Add a Business Feature

1. Fill in [feature-template.md](feature-template.md).
2. Put portable models/policies/contracts in Core, external implementations in Infrastructure, and WPF composition/presentation in App.
3. Implement from inner layer outward with matching tests.
4. For long work, support cancellation and progress; for persistence, define compatibility and recovery.
5. Run the [testing.md](../docs/testing.md) matrix and update only the owning long-term document.
