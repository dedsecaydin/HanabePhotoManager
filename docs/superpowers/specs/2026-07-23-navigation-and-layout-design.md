# Navigation and Layout Refinement Design

## Goal

Improve the desktop shell and the affected feature pages so navigation is faster, the controls follow one visual language, and large-screen layouts remain aligned. Preserve existing business behavior, commands, bindings, calendar behavior, and thumbnail sizing.

## Scope

- Make first-level business navigation reorderable by drag and drop and persist the order locally.
- Add a restrained line icon to every first-level destination.
- Add an Appearance setting for `Text`, `Icon`, or `Icon and text` navigation presentation.
- Refine the Browse, Map Photos, Compression, Connected Devices, and Settings layouts shown in the supplied screenshots.
- Correct the sidebar brand image so the complete image is visible.

Theme switching and Settings remain fixed in the sidebar footer and are not reorderable. Calendar layout and thumbnail-size controls retain their current sizing and behavior. No photo-management, map, compression, import, or device business logic changes are included.

## Architecture and Persistence

Introduce a small navigation item model owned by the App presentation layer. It carries the stable destination key, localized label, icon resource key, and current order. The shell binds the reorderable region to an observable collection; WPF code-behind handles pointer drag mechanics, while the ViewModel or settings service validates and persists the resulting stable-key order.

Extend the existing app settings payload with:

- `NavigationOrder`: ordered stable destination keys.
- `NavigationDisplayMode`: `Text`, `Icon`, or `IconAndText`.

Missing, duplicated, or unknown keys are normalized against the built-in destination list. New destinations are appended automatically, so older settings remain compatible.

## Shell Design

Use the existing icon geometry resource dictionary; add only missing line geometries and avoid a third-party dependency. Business destinations occupy one drag-reorderable list. The active item, hover, pressed, focus, and drag-target states reuse shared navigation tokens.

In `Icon` mode, icon content is centered and each item exposes its label through a tooltip and automation name. In `Text` and `IconAndText` modes, content remains left aligned. The footer controls keep their position and layout. The brand image uses proportional `Uniform` scaling with non-clipping bounds.

Keyboard navigation and activation remain available. Dragging changes order only after crossing another item's midpoint; Escape or an invalid drop leaves the order unchanged.

## Browse Page

Calendar and People remain independent cards on the left. The thumbnail-size control remains in the browse summary area. All other browse controls become one continuous workspace panel:

- search, retouch state, sorting, and rating form the primary filter row;
- manual category and custom tag actions form a secondary action row;
- intelligent recognition forms a tertiary action row.

The panel uses shared grid columns and responsive wrapping/minimum widths instead of tall nested cards. This removes the current vertical misalignment while preserving commands and bindings. Photo groups remain below the unified panel.

## Map and Compression Controls

Map mode selection becomes the shared segmented navigation control with equal-height states and theme brushes, replacing the white browser-tab appearance. Content below it switches exactly as before.

Compression target selection becomes one shared dark/light-aware input group: target type spans the group width, while numeric value and unit share a second row. Focus and selection use existing semantic brushes, and native white control chrome is removed through the shared ComboBox/TextBox styles.

## Appearance Settings Form

The background source, background display mode, glass intensity, and background actions become one compact settings group with consistent label spacing and shared input widths. The navigation display-mode selector sits in the same Appearance page as a separate clearly titled group. Combo boxes use the shared themed input style rather than oversized isolated outlines; labels remain visibly associated with their fields in both themes.

## Visual Closure Addendum

- Theme switching and Settings in the fixed sidebar footer receive the same restrained outline icons as first-level navigation.
- The sidebar brand header removes the `Hanabe Photos` text and presents only the complete square artwork, enlarged and centered without cropping.
- Appearance background source and display-mode controls use the shared themed input or segmented-control language; no native white ComboBox chrome remains.
- Compression target controls use the same themed input templates and never expose native white selection chrome.
- Browse category chips and the expanded filter/action workspace become one continuous panel. The file-search field receives the largest flexible share of the primary row.
- The new-custom-tag text field and `Create` action are removed from Browse; existing custom tags remain selectable and applicable.
- Horizontal divider lines between Browse action rows are removed; spacing and surface hierarchy provide separation.
- The unexplained `Adjust` label beside Browse conditions is removed. Disclosure and Reset remain the only controls in that header.

These changes preserve calendar and thumbnail-size behavior, existing tags, classification, recognition, compression, theme, and settings business flows. Removing the Browse tag-creation entry point does not delete persisted tags or the underlying tag-management service.

## Previously Reported Layout Fixes

- Connected Devices expands its content region to the disclosed device list and removes the large unused action-area height.
- Settings navigation uses the shell surface hierarchy, begins directly below the page divider, and reaches the available bottom edge without stray gaps.
- The Settings page removes unintended top spacing.
- Sidebar brand artwork is displayed completely rather than cropped.

## Error Handling and Accessibility

Settings persistence failures leave the current in-memory order usable and surface the existing non-blocking status/error mechanism. Invalid persisted navigation data falls back to the normalized built-in order. Icon-only navigation retains tooltips, automation labels, focus visibility, and keyboard activation. Dragging is optional; all destinations remain usable without it.

## Verification

- Add focused tests for navigation-order normalization, persistence round-trip, and display-mode loading/defaults.
- Add or extend resource tests for icon geometry and shared style keys.
- Run the Release solution build and complete test suite.
- Smoke-test Light/Dark theme switching, all three navigation display modes, drag reordering followed by restart, keyboard navigation, responsive Browse alignment, Map mode switching, Compression input focus, device disclosure, Settings edge alignment, and complete brand-image display.
