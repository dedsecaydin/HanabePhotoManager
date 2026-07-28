# Project Architecture

> **Purpose:** Define the authoritative project structure, dependency direction, runtime composition, MVVM boundaries, resource architecture, and principal data flows.  
> **Scope:** `HanabePhotoManager.sln`, production projects under `src/`, and their architectural relationship to `tests/`.  
> **Audience:** Developers and AI agents evaluating where a change belongs.  
> **References:** [`design-system.md`](design-system.md), [`resource-dictionary-structure.md`](resource-dictionary-structure.md)

## Table of Contents

- [System Shape](#system-shape)
- [Project Responsibilities](#project-responsibilities)
- [Dependency Rules](#dependency-rules)
- [MVVM Boundaries](#mvvm-boundaries)
- [Theme and Resource Architecture](#theme-and-resource-architecture)
- [Directory Responsibilities](#directory-responsibilities)
- [Principal Data Flows](#principal-data-flows)
- [Architecture Change Rules](#architecture-change-rules)

## System Shape

Hanabe Photo Manager is a Windows-only WPF application on .NET 8. `HanabePhotoManager.sln` contains three production projects and three corresponding xUnit projects. `Directory.Build.props` enables nullable reference types, implicit usings, C# 12, deterministic output, and warnings as errors across the solution.

```text
WPF Views / Windows
        ↓ binding and events
ViewModels + App orchestration services
        ↓ domain contracts and policies
HanabePhotoManager.Core
        ↑ implemented by
HanabePhotoManager.Infrastructure
```

The App composition root references both Core and Infrastructure. Core does not reference either outer project. Infrastructure references Core and implements its external-facing contracts.

## Project Responsibilities

### `HanabePhotoManager.Core`

Owns portable domain concepts and deterministic policies. Current examples include import models and planning, media grouping and classification, cloud contracts and scheduling policy, preview-loading policy, and throttled progress. Code here must not depend on WPF, Windows UI types, SQLite, HTTP provider details, or local application settings.

### `HanabePhotoManager.Infrastructure`

Owns implementations that cross process or system boundaries. Current responsibilities include verified file transfer and hashing, library directory initialization, import journals, persistent asset storage, SQLite cloud indexes, cloud queues and caches, OAuth/provider communication, and protected cloud-session storage.

Infrastructure may implement Core interfaces. It must not reference App or contain WPF presentation logic.

### `HanabePhotoManager.App`

Owns the WPF executable, composition, presentation, user interaction, and desktop-specific integrations. It contains pages and windows, ViewModels, app-local orchestration services, converters, WPF image/viewport behavior, themes, embedded model assets, and WebView2 map assets.

Some current services live in App because they use WPF imaging, Windows shell APIs, UI-facing state, or locally composed ML models. New portable business rules should still be placed in Core; external persistence or provider implementations should be placed in Infrastructure.

### `tests`

Each production project has a matching test project. Test placement follows the production owner: pure policies in Core tests, filesystem/cloud persistence in Infrastructure tests, and ViewModels, WPF resources, application services, and UI-adjacent behavior in App tests.

## Dependency Rules

- Allowed: `App → Core`, `App → Infrastructure`, `Infrastructure → Core`.
- Forbidden: `Core → Infrastructure`, `Core → App`, `Infrastructure → App`.
- Core interfaces define capabilities when business code must remain independent of storage or providers.
- Do not introduce a new project reference to bypass a misplaced class; relocate or extract the responsibility.
- Keep provider-specific types out of provider-neutral Core contracts.
- Keep WPF types out of Core and Infrastructure public APIs unless a documented architectural change explicitly replaces this boundary.

## MVVM Boundaries

Views declare layout, resources, bindings, commands, and visual states. ViewModels expose observable state and commands and coordinate use cases. Services perform filesystem, metadata, image, model, cloud, and operating-system work. Models and records carry data without UI behavior.

CommunityToolkit.Mvvm is the established mechanism for `ObservableObject`, generated observable properties, and relay commands. Prefer commands for user actions and bindings for state. Code-behind is acceptable only for view mechanics that require WPF object access—window lifecycle, focus, drag/drop, WebView2 hosting, viewport gestures, or binding-independent event adaptation. Business decisions and persistent state do not belong in code-behind.

The existing `MainWindowViewModel` is large and coordinates substantial application behavior. Do not expand it by default. New self-contained workflows should receive a focused ViewModel and service boundary; changes to existing behavior may remain local when extraction would create unrelated refactoring.

## Theme and Resource Architecture

`App.xaml` loads the active theme entry. `Themes/Themes/Light.xaml` and `Dark.xaml` compose ordered ResourceDictionaries for colors, semantic brushes, design tokens, typography, motion, and shared controls. `ThemeManager` replaces the theme dictionary at runtime and persists the selected preference.

Resource dependency direction is:

```text
Raw Colors → Semantic Brushes → Tokens / Typography / Motion → Control Styles → Views
```

Views consume semantic resources and shared component styles; they do not depend on raw theme colors. Both themes expose equivalent keys so controls never branch on the current theme. The detailed dictionary inventory is recorded in [`resource-dictionary-structure.md`](resource-dictionary-structure.md). All visual values, token semantics, and UI state requirements belong exclusively to [`design-system.md`](design-system.md).

## Directory Responsibilities

| Path | Responsibility |
|---|---|
| `src/HanabePhotoManager.Core/Imports` | Media discovery models, grouping, classification, and import planning |
| `src/HanabePhotoManager.Core/Cloud` | Provider-neutral cloud models, contracts, authentication abstractions, and scheduling |
| `src/HanabePhotoManager.Core/Performance` | Portable loading and progress policies |
| `src/HanabePhotoManager.Infrastructure/Files` | Durable and verified filesystem operations |
| `src/HanabePhotoManager.Infrastructure/Cloud` | Cloud provider, cache, queue, index, OAuth, and session implementations |
| `src/HanabePhotoManager.App/ViewModels` | General screen and workflow presentation state |
| `src/HanabePhotoManager.App/Services` | App-composed, UI-facing, imaging, ML, metadata, and Windows services |
| `src/HanabePhotoManager.App/Cloud` | Cloud page and cloud presentation orchestration |
| `src/HanabePhotoManager.App/Compression` | Compression page, ViewModel-facing workflow, discovery, planning, and execution |
| `src/HanabePhotoManager.App/Watermark` | Watermark page, layout policy, input discovery, and export |
| `src/HanabePhotoManager.App/Map` | Map page, WebView2 bridge, and bundled Leaflet assets |
| `src/HanabePhotoManager.App/Contest` | Contest pages, picker window, and contest ViewModel |
| `src/HanabePhotoManager.App/Themes` | Theme entries, resources, tokens, typography, motion, and shared control styles |
| `src/HanabePhotoManager.App/Models` | App-facing data plus bundled ML model assets and notices |
| `tests/*` | Tests matching the production project and responsibility |
| `tools` | Repository automation such as clean release publishing |

## Principal Data Flows

### Import and organization

The App discovers candidate files and captures user choices. Core classifies media, groups sidecars, resolves dates, and builds an import plan. Infrastructure hashes, transfers, journals, and stores files. ViewModels receive progress and results and update observable UI state.

### Library browsing and metadata

App services scan media, read thumbnails/EXIF, maintain app settings and metadata, and feed ViewModels. ViewModels expose filtered or grouped collections to WPF views. Long-running work is cancelable and reports throttled progress where applicable.

### Local intelligence

App services load bundled ONNX/OpenCV assets for classification, face embedding, clustering, and search. Checkpoint or metadata services persist resumable state. ViewModels present analysis progress and results; model files and third-party notices remain under `App/Models`.

Face recognition uses two isolated ONNX Runtime pipelines. The default
YuNet + SFace identity retains the version-1 people-album migration path.
ArcFace R100 is opt-in and accepts only user-supplied detector and recognizer
paths after an explicit license confirmation; no ArcFace weights are bundled
or downloaded. People-album snapshots persist engine, model fingerprint,
embedding version, and threshold identity. A mismatched identity is rejected,
and non-default engines use separate storage files, so vectors from different
models can never be compared or merged.

### Cloud

Core defines provider-neutral models, stores, authentication contracts, and scheduling policy. Infrastructure supplies persistent stores and provider implementations. The cloud App module composes these capabilities into navigation and transfer states. Credentials and sessions must remain outside source control and logs.

### Theme switching

The user selection reaches `ThemeManager`, which swaps the composed theme dictionary and persists the preference. Existing bindings resolve equivalent resource keys from the new dictionary without ViewModel changes.

## Architecture Change Rules

An architecture change is any new project dependency, cross-layer contract, persistent data format, provider boundary, composition mechanism, or resource-loading order. Analyze and document it before implementation, update this file when accepted, and add tests at the owning layer. UI appearance changes do not update this document; they follow [`design-system.md`](design-system.md).
