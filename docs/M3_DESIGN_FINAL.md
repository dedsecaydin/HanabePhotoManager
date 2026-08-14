# HanabePhoto M3 设计定稿（2026-08-14 用户确认）

> 状态：用户拍板，替代之前"克制桌面工具风"方向。这是 UI/UX 重构的**新基准**，所有后续阶段（70%+）以此为准。
> 来源：三个 HTML 预设计变体（D:\hanabephoto\sketches\001-m3-dynamic-color / 002-m3-surface-tonal / 003-m3-tool-density）→ 用户选定：**排版用变体 001，三套配色都要**。

## 一、设计方向（定稿）

**Material Design 3 × Codex Desktop × Lightroom**，但**浓烈 M3**（不是之前克制的桌面工具风）：

- **大圆角 surface**（28dp 级容器：Navigation Rail / 主区 / Inspector / FAB / Chips / 搜索框全大圆角）
- **动态色彩 tonal 层次**（primary/secondary/tertiary 三色 palette 各司其职，容器用同色相不同明度）
- **彩色状态层**（hover/pressed 半透明色叠加，State Layers）
- **克制动效** 150/180/220ms（M3 motion easing）

## 二、排版（固定，来自变体 001）

```
┌────────┬───────────────────────────────────────┬──────────┐
│ Rail   │ Topbar（标题+副标题+搜索框）           │          │
│ (88px) ├───────────────────────────────────────┤          │
│        │ Workspace（Chips 筛选 + 照片网格）      │ Inspector│
│ 7项    │                                       │ (320px)  │
│ 导航    │                                       │ EXIF+操作 │
│        ├───────────────────────────────────────┤          │
│        │ Statusbar（索引状态+进度条+百分比）      │          │
└────────┴───────────────────────────────────────┴──────────┘
      FAB（右下角，导入照片）
```

- **Navigation Rail**：88px 宽，7 项（主页/图库/人物/相册/地图/网盘/设置），图标+文字纵向，选中态 = secondary-container + primary-container 圆icon + state layer
- **Topbar**：大圆角 surface-container-low，标题 + 副标题（数量/日期/扫描时间）+ 胶囊搜索框
- **Workspace**：surface-container-lowest，Chips 筛选（全部/今天/本月/视频/收藏），照片网格 `repeat(auto-fill, minmax(140px,1fr))`
- **Inspector**：320px，surface-container-low，选中照片 EXIF（尺寸/时间/相机/镜头/ISO/人物）+ 操作 Chips（加入相册/导出/收藏）
- **Statusbar**：44px，surface-container，索引状态 + 进度条 + 百分比
- **FAB**：56px 圆形，primary 色，右下角，导入照片

## 三、六套主题（3 配色 × 浅/深色）

应用内可切换（设置里选），**默认：动态色彩 · 浅色**。

### 1. 动态色彩（Dynamic Color）—— 靛蓝/暖橙/淡紫
```css
/* 浅色 */
--primary: #4355B9;            --primary-container: #DEE0FF;
--secondary: #5B5D72;          --secondary-container: #E0E1F9;
--tertiary: #7A5700;           --tertiary-container: #FFDF9E;
--surface: #FBF8FF;            --surface-dim: #DAD8E4;
--surface-container: #EFECF7;  --surface-container-high: #E9E7F1;
--on-surface: #1A1B21;         --on-surface-variant: #45464F;
--outline: #75767F;            --outline-variant: #C5C5CF;

/* 深色（M3 dark scheme 推导，仅示意，落地时按 M3 tonal 规则算） */
--primary: #BCC3FF;            --primary-container: #2A3585;
--secondary: #C4C6DE;          --secondary-container: #434459;
--tertiary: #FFC34F;           --tertiary-container: #5E4100;
--surface: #121318;            --surface-dim: #121318;
--surface-container: #1D1E24;  --surface-container-high: #27282F;
--on-surface: #E3E1E9;         --on-surface-variant: #C6C5D0;
--outline: #90919B;            --outline-variant: #45464F;
```

### 2. 森林绿（Surface-Tonal）—— 绿系
```css
/* 浅色 */
--primary: #1D6B50;            --primary-container: #A8F2CD;
--secondary: #4F6357;          --secondary-container: #D2E8DA;
--tertiary: #3E6373;           --tertiary-container: #C2E8FB;
--surface: #FCFDF7;            --surface-dim: #DCE3DB;
--surface-container: #F0F5EE;  --surface-container-high: #EAEFE9;
--on-surface: #1B1D1B;         --on-surface-variant: #444844;
--outline: #757A75;            --outline-variant: #C4C9C3;

/* 深色（示意，落地按 M3 规则算） */
--primary: #8CD5B2;            --primary-container: #004930;
--secondary: #B5CCBD;          --secondary-container: #374B3F;
--tertiary: #A6CCDE;           --tertiary-container: #244B5B;
--surface: #121412;            --surface-dim: #121412;
--surface-container: #1D201D;  --surface-container-high: #272A27;
--on-surface: #E1E3DE;         --on-surface-variant: #C2C8C2;
--outline: #8C928C;            --outline-variant: #434943;
```

### 3. 紫罗兰（Tool Density）—— 紫系
```css
/* 浅色 */
--primary: #8B4AA6;            --primary-container: #F7D8FF;
--secondary: #6B5D71;          --secondary-container: #F3DFF8;
--tertiary: #006A6A;           --tertiary-container: #9CF0F0;
--surface: #FEF7FF;            --surface-dim: #DED8E1;
--surface-container: #F5ECF7;  --surface-container-high: #EFE6F1;
--on-surface: #1D1B1E;         --on-surface-variant: #49454E;
--outline: #7A757E;            --outline-variant: #CAC4CF;

/* 深色（示意，落地按 M3 规则算） */
--primary: #E3AEF7;            --primary-container: #6F2B89;
--secondary: #D9C2DE;          --secondary-container: #524359;
--tertiary: #77D3D3;           --tertiary-container: #00504F;
--surface: #151316;            --surface-dim: #151316;
--surface-container: #201E21;  --surface-container-high: #2B282C;
--on-surface: #E7E1E8;         --on-surface-variant: #CAC4CF;
--outline: #948F98;            --outline-variant: #49454E;
```

## 四、落地要求（铁律）

1. **WPF 实现**：主题切换机制沿用现有 `ThemeManager`（Light/Dark 已支持）→ 扩展为「配色 × 明暗」6 套组合。Token 语义键不变（`Brush.Primary` / `Brush.Surface.Container` 等），**只换主题资源字典的值**，页面不写死颜色。
2. **排版按变体 001**：Rail 88px 7 项（现有 Sidebar 232px → 改 88px Navigation Rail，图标+文字竖排）、Topbar、Workspace 网格、Inspector 320px、Statusbar、FAB。
3. **大圆角**：容器 28dp、卡片 12-16dp、chip/按钮 full/8-12dp（对应现有 Radius Token 更新）。
4. **状态层**：hover/pressed 用半透明叠加（现有 `Button.Ghost` 语义保留，但视觉改为 M3 state layer）。
5. **动效**：150/180/220ms 不变。
6. **保留全部业务功能**：现有所有页面（浏览/人物/相册/导入/工具/地图/网盘/设置）都要在 M3 排版下可用，只改视觉不改逻辑。
7. **构建/测试全绿**：dotnet build 0 警告 0 错误 + dotnet test 全绿（当前 908 个测试）。
8. 默认主题：**动态色彩 · 浅色**。

## 五、预设计文件（参考）

- `D:\hanabephoto\sketches\001-m3-dynamic-color\index.html` — 排版基准
- `D:\hanabephoto\sketches\002-m3-surface-tonal\index.html` — 配色 2 参考
- `D:\hanabephoto\sketches\003-m3-tool-density\index.html` — 配色 3 参考

## 六、阶段计划（建议）

- **M3-1 主题基建**：ThemeManager 扩展 6 套主题 + 默认动态色彩浅色 + 设置里切换（三套配色 × 明暗）
- **M3-2 Shell 改版**：Sidebar 232px → Navigation Rail 88px，Topbar/Statusbar 大圆角
- **M3-3 浏览页改版**：Workspace 网格 + Chips + FAB + Inspector 320px
- **M3-4 其余页面适配**：人物/相册/导入/工具/地图/网盘/设置
- **M3-5 回归**：构建 + 908 测试 + 大库实测 + 截图（6 主题）
