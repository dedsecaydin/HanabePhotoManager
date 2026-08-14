# Hanabe Photo Manager

> A local photo management desktop app built for photography workflows · Semantic search / Face recognition / Treemap browsing / Batch tools

Hanabe Photo Manager is a Windows desktop photo management tool designed for photographers — especially Cosplay photographers. It combines **Lightroom-style organization**, **Google Photos-style smart search**, and a **Material Design 3 interface** in a local application — your photos always stay on your own hard drive.

![version](https://img.shields.io/badge/version-0.3.0--alpha-blue)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![WPF](https://img.shields.io/badge/UI-WPF-lightgrey)
![tests](https://img.shields.io/badge/tests-926%20passing-brightgreen)
![License](https://img.shields.io/badge/license-MIT-green)

**Read this in:** [English](README.md) · [日本語](README.ja.md) · [简体中文](README.zh-CN.md)

---

## ✨ Features

### 📂 Smart Browsing (Photo Wall)
- **Three views**: Treemap / Grid / Waterfall, with virtualized rendering — smooth scrolling even with tens of thousands of photos
- **Smart filters**: category (RAW / JPG / retouched / video / assets), retouch status, rating, file type
- **Date calendar**: quickly locate photos by capture date
- **Ctrl+scroll** to zoom thumbnails instantly

### 🔍 Semantic Search (Local AI)
- **CLIP model semantic retrieval**: type "red dress", "night portrait", "by the river with friends" and the photo wall shows relevant results directly — pure local ONNX inference, photos never leave your machine
- Unified search across file names / paths / semantic descriptions, with incremental indexing that shows results as it indexes

### 👤 Face Recognition
- **YuNet face detection + SFace face recognition** (OpenVINO), builds local people albums
- Merge / rename people, find similar photos by face
- Virtualized loading for people with 100+ photos — smooth scrolling

### 🗺️ Map Photos
- Reads EXIF GPS and browses photos by capture location on a map
- Manual tagging mode: pick a point on the map to auto-fill latitude/longitude, Ctrl/Shift multi-select for batch tagging

### 🧰 Batch Tools
- **Image compression** / **collage** / **watermark** batch processing
- **WeChat sending**: three-step flow (detect WeChat → locate target → batch send)
- **Duplicate detection**: SHA-256 exact dedup + similarity review, smart dedup on import

### 📦 Import & Albums
- Batch import (copy / move after verification), automatic category sorting
- Custom albums / folder references, card-flow browsing with grid/list toggle
- Retouched-directory read-only protection (prevents accidentally overwriting originals)

### ☁️ Cloud Drive & Submissions
- Embedded WebView2 cloud drive client (Baidu Cloud OAuth) with transfer queue management
- Submission / showcase projects: WebView2 browser + local photo integration

### 🎨 Material Design 3 Design System
- **6 themes**: Dynamic Color (indigo) / Forest Green / Violet × light/dark, one-click switching in-app
- A complete semantic-token-driven design system (28dp large radii / state layers / 150-220ms motion)
- Modern three-pane layout: Navigation Rail + Workspace + Inspector

---

## 📸 Screenshots

| Browser (Dynamic Color, Light) | Browser (Dark) |
|---|---|
| ![Browser light](docs/screenshots/m3-browser-dynamic-light.png) | ![Browser dark](docs/screenshots/m3-browser-dynamic-dark.png) |

| Face Search | Albums |
|---|---|
| ![Face search](docs/screenshots/m3-facesearch-light.png) | ![Albums](docs/screenshots/m3-albums-light.png) |

| Import | Settings |
|---|---|
| ![Import](docs/screenshots/m3-import-light.png) | ![Settings](docs/screenshots/m3-settings-light.png) |

---

## 🚀 Quick Start

### Requirements
- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build & Run

```bash
git clone https://github.com/dedsecaydin/HanabePhotoManager.git
cd HanabePhotoManager

# Build
dotnet build HanabePhotoManager.sln -c Release

# Run
dotnet run --project src/HanabePhotoManager.App

# Test (917 unit tests)
dotnet test HanabePhotoManager.sln
```

### Publish (self-contained single directory)

```bash
dotnet publish src/HanabePhotoManager.App -c Release -r win-x64 --self-contained true
```

---

## 🏗️ Project Structure

```
src/
├── HanabePhotoManager.Core/          # Domain models + service interfaces (search, performance policies, import planning)
├── HanabePhotoManager.Infrastructure/# Infrastructure (SQLite indexes, CLIP inference, file transfer, Baidu Cloud, SHA-256)
└── HanabePhotoManager.App/           # WPF application layer (pages, ViewModels, design system, controls)
    ├── Browsing/                     # Virtualized Treemap/Grid/Waterfall browsing
    ├── Search/                       # Semantic search integration
    ├── People/                       # Face recognition & albums
    ├── Compression/ Watermark/       # Batch tools
    ├── Cloud/                        # Cloud drive client
    └── Themes/                       # Six Material Design 3 theme tokens
```

---

## 🛠️ Tech Stack

| Layer | Technology |
|---|---|
| Framework | .NET 8 · WPF · MVVM (CommunityToolkit.Mvvm) |
| Semantic search | ONNX Runtime · CLIP model · SQLite vector index |
| Face recognition | OpenCvSharp4 · YuNet · SFace (OpenVINO) |
| Cloud drive | WebView2 · OAuth2 |
| Testing | xUnit · 917+ unit tests |

---

## 🧠 How It Was Built (A Vibe Coding Log)

This project is a complete **vibe coding (AI-assisted development)** case study — a photographer supplied the requirements, and AI agents handled the architecture, coding, testing, and iteration, coordinated remotely via WeChat from start to finish.

### Development Approach
- **Requirement-driven**: the photographer (not a full-time developer) described real workflow needs (semantic photo search, people organization, batch tools), and AI implemented them
- **Multi-agent division of labor**:
  - **Codex CLI (gpt-5.6-terra)**: high-complexity features (treemap browsing, virtualization, semantic search integration)
  - **dsh / DeepSeek subagents**: UI refactoring, feature-page design, test completion
  - **Hermes (orchestrator)**: requirement analysis, task breakdown, review, release, operations
- **Design-first**: every major redesign started with HTML design mockups; XAML was only implemented after the user confirmed the direction
- **Test-driven**: 917 unit tests guard the project (design-system regression tests automatically catch UI violations such as hardcoded colors or out-of-spec corner radii)
- **Iteration cadence**: 20% → 60% → 70% → feature-page redesigns (People/Albums/Import/Settings/Tools/Map/Cloud) → 6 themes

### Key Decisions
| Decision | Reason |
|---|---|
| Local-first (no cloud) | Photo work privacy + no need to upload tens of thousands of photos |
| CLIP + ONNX semantic search | Local inference, small and fast models |
| Custom treemap virtualization | Smooth scrolling with tens of thousands of photos — a view Lightroom doesn't have |
| Material Design 3 design system | Modern, restrained visual language, full theme adaptation via semantic tokens |
| Three-pane layout (Rail + Workspace + Inspector) | Information density inspired by Codex Desktop / Lightroom |

### Timeline
- **2026-08-09**: Project kickoff, semantic search integration (incremental indexing, results while indexing)
- **2026-08-10**: Browser virtualization (11,739 items in treemap/grid, millisecond-level)
- **2026-08-12**: Design system alignment (tokens, motion spec)
- **2026-08-14**: Bold M3 overhaul (6 themes) + full feature-page redesign + 917 tests green

---

## ☕ Support the Author

If you find this tool useful, feel free to buy the author a coffee/cola ☕🥤

<img src="docs/screenshots/wechat-sponsor-qr.jpg" width="240" alt="微信赞赏码 · WeChat Sponsor QR">

- **WeChat Sponsor QR** — scan it with WeChat to send a tip (微信赞赏码)
- [Afdian (爱发电)](https://afdian.com/a/hanabededsec) ｜ If you find it useful, support me on Afdian!

Every cup of coffee is the biggest motivation for continued development!

---

## 📄 License

MIT License — free to use, modify, and distribute.

---

*Made with ❤️ by HANABE (花火) · A photo manager for photographers, by a photographer.*
