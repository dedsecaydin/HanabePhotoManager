# AI Architecture Map

> **Purpose:** Provide a fast location map for code and documentation.  
> **Scope:** Current repository directories and allowed dependency direction.  
> **Audience:** AI agents locating the owner of a requested change.  
> **References:** [architecture.md](../docs/architecture.md)

## Dependency Map

```text
App ─────────────→ Core
 │                 ↑
 └→ Infrastructure ┘
```

This is a lookup aid. [architecture.md](../docs/architecture.md) is the authority for dependency and MVVM rules.

## Code Map

| Need to change | Start here | Tests |
|---|---|---|
| Import models/grouping/plans | `src/HanabePhotoManager.Core/Imports` | `tests/HanabePhotoManager.Core.Tests/Imports` |
| Cloud contracts/scheduling | `src/HanabePhotoManager.Core/Cloud` | Core cloud tests |
| File transfer/hash/library/journal | `src/HanabePhotoManager.Infrastructure/Files` | Infrastructure file tests |
| Cloud provider/cache/queue/index/session | `src/HanabePhotoManager.Infrastructure/Cloud` | Infrastructure cloud tests |
| Main presentation state | `src/HanabePhotoManager.App/ViewModels` | App tests |
| Metadata, thumbnails, people, ML, Windows integrations | `src/HanabePhotoManager.App/Services` | App tests |
| Cloud UI | `src/HanabePhotoManager.App/Cloud` | App cloud tests |
| Compression | `src/HanabePhotoManager.App/Compression` | Compression App tests |
| Watermark | `src/HanabePhotoManager.App/Watermark` | Watermark App tests |
| Map/WebView2 | `src/HanabePhotoManager.App/Map` | Map App tests + smoke |
| Contest | `src/HanabePhotoManager.App/Contest` | App tests |
| Shared UI resources | `src/HanabePhotoManager.App/Themes` | Design-system/theme/resource App tests |
| Release packaging | `tools/Publish-Clean.ps1` | Release verification |

## Documentation Map

| Question | Authority |
|---|---|
| Where does code belong? | [architecture.md](../docs/architecture.md) |
| May I create/extend this component? | [components.md](../docs/components.md) |
| How is it written? | [coding-style.md](../docs/coding-style.md) |
| What is the UI rule? | [design-system.md](../docs/design-system.md) |
| What process applies? | [workflow.md](../docs/workflow.md) |
| What must pass? | [testing.md](../docs/testing.md) |
| How is it released? | [release.md](../docs/release.md) |

Snapshots such as component inventory, resource-dictionary structure, audits, specs, and plans provide evidence/history but do not override these authorities.
