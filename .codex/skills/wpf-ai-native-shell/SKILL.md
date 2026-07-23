---
name: wpf-ai-native-shell
description: Project-specific WPF UI workflow for Hanabe Photo Manager. Use for MainWindow, App Shell, Sidebar/navigation, top regions, home layout, integrated native-desktop visuals, settings-center layout, DPI and text truncation, Light/Dark theme consistency, or Visual QA work in the HanabePhotoManager repository.
---

# Hanabe WPF AI-native Shell

Create a continuous, restrained AI-native desktop shell for Hanabe Photo Manager while preserving all application behavior.

## Verify the repository first

Before analysis or file creation, confirm the current root contains all of:

- `HanabePhotoManager.sln`
- `AGENTS.md`
- `docs/design-system.md`
- `docs/components.md`
- `docs/coding-style.md`
- `src/`
- `tests/`

If any entry is missing, stop and report the current absolute path. Never generate a generic template in the wrong or empty directory.

## Load project authority

Read in this order:

1. `AGENTS.md`
2. `docs/design-system.md`
3. `docs/components.md`
4. `docs/coding-style.md`
5. `docs/testing.md`
6. `src/HanabePhotoManager.App/MainWindow.xaml`
7. Relevant dictionaries under `src/HanabePhotoManager.App/Themes/`

Treat `docs/design-system.md` as the sole UI authority. Prefer existing Design Tokens, ResourceDictionaries, and shared styles. Add semantic tokens only when the existing contract cannot express a required layer; keep Light/Dark keys identical and update the owning documentation or inventory.

## Preserve behavior

Do not modify ViewModels, Commands, Binding expressions, APIs, data structures, event handlers, or business flows. Do not perform unrelated refactoring. Before editing a large XAML view, capture Binding, Command, and event-handler inventories; compare them after editing.

## Design the shell

- Make Sidebar, top region, and main content one continuous visual system.
- Use a shared shell background, natural whitespace, aligned typography, restrained transparency, and light separators.
- Keep the soft, light, low-saturation, neutral direction; never flatten the UI into a generic pure-white admin page.
- Remove, merge, or weaken outer Cards, Borders, Panels, and nested containers that fragment the shell.
- Do not wrap every module in a large rounded Card. Preserve small Cards with independent semantics, including thumbnails, device items, and folder items.
- Establish hierarchy through titles, spacing, background layers, opacity, and fine separators before adding effects.
- Keep the result native-desktop, coherent, professional, and atmospheric without resembling a game launcher.

Use Codex Desktop, ChatGPT Desktop, and Linear only as atmosphere references. Do not copy their branded components or layouts.

## Control visual effects

Allow effects only through shared semantic resources and unified tokens:

- Use translucent backgrounds or restrained glass material for shell chrome, overlays, dialogs, popups, selection, or a genuinely emphasized region.
- Use background blur only when the existing WPF implementation can support it without new business behavior or unstable performance; otherwise use tokenized translucent material.
- Use soft, low-saturation gradients for environmental depth, never cheap multicolor gradients or repeated component decoration.
- Keep ordinary content shadow-free or use only a very light separation layer. Use medium shadows for floating surfaces, dialogs, and popups. Reserve stronger emphasis for one exceptional display region.
- Use highlight strokes for material edges and glow only for focus, selection, or special state. Never make glow a universal border.
- Preserve text contrast, Light/Dark parity, DPI behavior, and performance.

Never hardcode page-level colors, fonts, radii, shadows, gradients, blur values, or spacing. Never add duplicate page-level shared Styles or copied ControlTemplates.

## Execute the workflow

1. Analyze MainWindow, shell grid, Sidebar, top region, home container, and relevant shared resources.
2. Identify fragmenting Cards, Borders, Panels, nested wrappers, and repeated effects.
3. Decide which existing glass, gradient, shadow, and glow layers are meaningful and which are decorative repetition.
4. Present a short implementation plan.
5. Make the smallest coherent resource and XAML changes.
6. Prioritize structure, background hierarchy, whitespace, alignment, and typography.
7. Retain or refine effects only at meaningful emphasis and floating layers.
8. Preserve semantic small Cards and all behavior-bearing attributes.
9. Verify and iterate when the result still reads as a card-collage dashboard or has excessive effects.

## Verify completion

Run from the project root and stop on a mandatory failure:

```powershell
dotnet build HanabePhotoManager.sln -c Release /warnaserror
dotnet test HanabePhotoManager.sln -c Release --no-build
dotnet publish src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release -r win-x64
```

Use the repository's formal publish script instead when `docs/testing.md` or `docs/release.md` requires it.

Perform Visual QA on fresh published output and save new screenshots without overwriting older evidence. Check:

- Sidebar, top region, and main content continuity
- Remaining dashboard-card collage
- Unified content background, whitespace, alignment, and title hierarchy
- Soft, neutral, low-saturation palette
- Effect restraint and text readability
- Light and Dark themes
- Small supported window and common DPI scales
- Long Chinese text, clipping, overflow, truncation, and misalignment
- Hover, Pressed, Focus, Disabled
- Loading, Empty, Error
- Navigation and all existing interactions

Report automated evidence separately from manual checks. Never claim a state was verified when it was not reachable.
