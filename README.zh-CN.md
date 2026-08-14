# Hanabe Photo Manager

> 为摄影工作流打造的本地方照片管理桌面应用 · 语义搜索 / 人物识别 / 树图浏览 / 批量工具

Hanabe Photo Manager 是一个面向摄影师（尤其 Cosplay 摄影）的 Windows 桌面照片管理工具。它把 **Lightroom 式的组织能力**、**Google Photos 式的智能搜索**与 **Material Design 3 的现代界面**结合在本地应用中——你的照片永远留在你的硬盘上。

![version](https://img.shields.io/badge/version-0.3.0--alpha-blue)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![WPF](https://img.shields.io/badge/UI-WPF-lightgrey)
![tests](https://img.shields.io/badge/tests-926%20passing-brightgreen)
![License](https://img.shields.io/badge/license-MIT-green)

**Read this in:** [English](README.md) · [日本語](README.ja.md) · [简体中文](README.zh-CN.md)

---

## ✨ 功能特性

### 📂 智能浏览（照片墙）
- **三种视图**：树图（Treemap）/ 网格 / 瀑布流，虚拟化渲染，万张照片毫秒级滚动
- **智能筛选**：分类（RAW/JPG/修后/视频/素材）、修图状态、评分、文件类型
- **日期日历**：按拍摄日期快速定位
- **Ctrl+滚轮** 即时缩放缩略图

### 🔍 语义搜索（本地 AI）
- **CLIP 模型语义检索**：输入「红色裙子」「夜景人像」「和朋友在江边」，照片墙直接显示相关结果——纯本地 ONNX 推理，照片不上云
- 文件名 / 路径 / 语义描述合并搜索，增量索引边搜边出

### 👤 人物识别
- **YuNet 人脸检测 + SFace 人脸识别**（OpenVINO），本地建立人物相册
- 人物合并 / 重命名，按脸查找相似照片
- 百张以上人物照片虚拟化加载，滚动流畅

### 🗺️ 地图照片
- 读取 EXIF GPS，在地图上按拍摄地点浏览照片
- 手动标记模式：地图取点自动填经纬度，Ctrl/Shift 多选批量标注

### 🧰 批量工具
- **图片压缩** / **拼图** / **水印** 批量处理
- **微信发送**：三步流程（检测微信 → 定位目标 → 批量发送）
- **重复检测**：SHA-256 精确查重 + 相似审查，导入时智能去重

### 📦 导入与相册
- 批量导入（复制 / 校验后移动），分类自动归位
- 自定义相册 / 文件夹引用，卡片流浏览 + 网格/列表切换
- 修后只读保护（防止误覆盖原图）

### ☁️ 网盘
- 内嵌 WebView2 网盘客户端（百度云 OAuth），传输队列管理

### 🎨 Material Design 3 设计系统
- **6 套主题**：动态色彩（靛蓝）/ 森林绿 / 紫罗兰 × 浅色/深色，应用内一键切换
- 语义 Token 驱动的完整设计系统（28dp 大圆角 / 状态层 / 150-220ms 动效）
- Navigation Rail + Inspector + FAB 现代三栏布局

---

## 📸 界面截图

| 浏览页（动态色彩·浅色） | 浏览页（深色） |
|---|---|
| ![浏览页浅色](docs/screenshots/m3-browser-dynamic-light.png) | ![浏览页深色](docs/screenshots/m3-browser-dynamic-dark.png) |

| 人物查找 | 相册 |
|---|---|
| ![人物查找](docs/screenshots/m3-facesearch-light.png) | ![相册](docs/screenshots/m3-albums-light.png) |

| 导入 | 设置 |
|---|---|
| ![导入](docs/screenshots/m3-import-light.png) | ![设置](docs/screenshots/m3-settings-light.png) |

---

## 🚀 快速开始

### 环境要求
- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 构建运行

```bash
git clone https://github.com/dedsecaydin/HanabePhotoManager.git
cd HanabePhotoManager

# 构建
dotnet build HanabePhotoManager.sln -c Release

# 运行
dotnet run --project src/HanabePhotoManager.App

# 测试（917 个单元测试）
dotnet test HanabePhotoManager.sln
```

### 发布（自包含单目录）

```bash
dotnet publish src/HanabePhotoManager.App -c Release -r win-x64 --self-contained true
```

---

## 🏗️ 项目结构

```
src/
├── HanabePhotoManager.Core/          # 领域模型 + 服务接口（搜索、性能策略、导入规划）
├── HanabePhotoManager.Infrastructure/# 基础设施（SQLite 索引、CLIP 推理、文件传输、百度云、SHA-256）
└── HanabePhotoManager.App/           # WPF 应用层（页面、ViewModel、设计系统、控制）
    ├── Browsing/                     # 树图/网格/瀑布流虚拟化浏览
    ├── Search/                       # 语义搜索集成
    ├── People/                       # 人物识别与相册
    ├── Compression/ Watermark/       # 批量工具
    ├── Cloud/                        # 网盘客户端
    └── Themes/                       # Material Design 3 六套主题 Token
```

---

## 🛠️ 技术栈

| 层 | 技术 |
|---|---|
| 框架 | .NET 8 · WPF · MVVM (CommunityToolkit.Mvvm) |
| 语义搜索 | ONNX Runtime · CLIP 模型 · SQLite 向量索引 |
| 人脸识别 | OpenCvSharp4 · YuNet · SFace (OpenVINO) |
| 网盘 | WebView2 · OAuth2 |
| 测试 | xUnit · 917+ 单元测试 |

---

## 🧠 实现过程（Vibe Coding 实录）

这个项目是一个完整的 **vibe coding（AI 辅助开发）** 实践案例——由摄影师提出需求，AI 智能体负责架构、编码、测试与迭代，全程通过微信远程指挥完成。

### 开发方式
- **需求驱动**：摄影师（非全职开发者）提出真实工作流需求（语义搜图、人物整理、批量工具），AI 落地实现
- **多智能体分工**：
  - **Codex CLI (gpt-5.6-terra)**：高复杂度功能开发（树图浏览、虚拟化、语义搜索集成）
  - **dsh / DeepSeek 子代理**：UI 重构、功能页设计、测试补全
  - **Hermes（编排者）**：需求分析、任务拆分、评审、发布、运维
- **设计先行**：每个大改版先做 HTML 预设计 mockup，用户确认方向后才落地 XAML
- **测试驱动**：917 个单元测试护航（设计系统回归测试会自动抓出硬编码颜色、圆角越界等 UI 违禁项）
- **迭代节奏**：20% → 60% → 70% → 功能页重设计（人物/相册/导入/设置/工具/地图/网盘）→ 6 套主题

### 关键决策
| 决策 | 原因 |
|---|---|
| 本地优先（不上云） | 摄影作品隐私 + 万张照片无需上传 |
| CLIP + ONNX 语义搜索 | 本地推理，模型小、速度快 |
| 自研树图虚拟化 | 万张照片滚动不卡，Lightroom 没有的视图 |
| Material Design 3 设计系统 | 现代、克制的视觉语言，语义 Token 全主题适配 |
| 三栏布局（Rail + 工作区 + Inspector） | 参考 Codex Desktop / Lightroom 的信息密度 |

### 演进时间线
- **2026-08-09**：项目起步，语义搜索集成（增量索引、边搜边出）
- **2026-08-10**：浏览页虚拟化（树图/网格 11739 项毫秒级）
- **2026-08-12**：设计系统对齐（Token 化、动效规范）
- **2026-08-14**：M3 浓烈版大改（6 套主题）+ 全部功能页重设计 + 917 测试全绿

---

## ☕ 支持作者

如果你觉得这个工具有用，欢迎请作者喝一杯咖啡/可乐 ☕🥤

- **微信赞赏码**：
  <img src="docs/screenshots/wechat-sponsor-qr.jpg" width="240" alt="微信赞赏码 · WeChat Sponsor QR">
- [爱发电](https://afdian.com/a/hanabededsec) ｜ 如果你觉得有用，欢迎来爱发电支持！

你的每一杯咖啡都是持续开发的最大动力！

## 📄 License

MIT License — 自由使用、修改与分发。

---

*Made with ❤️ by HANABE (花火) · A photo manager for photographers, by a photographer.*
