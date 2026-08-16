# HanabePhotoManager 安装器外壳（WixStdBA 主题）

本目录是 **Burn Bundle 的 Bootstrapper UI 外壳（外观）**，基于 WiX v5 的
`WixStandardBootstrapperApplication`（WixStdBA）+ ThmUtil 主题机制定制，微信 / Discord
现代软件安装风格。**本目录只负责外观，不包含任何应用内容。**

## 文件清单

| 文件 | 作用 |
| --- | --- |
| `HanabeTheme.xml` | 自定义主题（页面 / 控件 / 字体 / 颜色 / 图片引用） |
| `HanabeTheme.wxl` | 简体中文（zh-CN）本地化文案，含所有错误消息的中文翻译 |
| `logo.png` | 品牌 Logo（128×128，紫色圆角方形 + 白色照片图标） |
| `button-primary*.png` | 主按钮（品牌紫 `#8B4AA6`，170×44，圆角矩形；normal / hover / selected） |
| `button-secondary*.png` | 次按钮（浅灰，120×44，圆角矩形；normal / hover / selected） |
| `progressbar.png` | 进度条贴图（4×12：左缘/已完成=紫，未完成/右缘=浅灰） |

## 主题结构

主题文件是 ThmUtil XML（命名空间 `http://wixtoolset.org/schemas/v4/thmutil`）。窗口
620×430，页面沿用 **WixStdBA 固定的页面名**（不能改名）：

- `Install` —— 欢迎 / 安装页（含 RTF 许可 + 同意勾选）
- `Options` —— 安装目录选择页
- `Progress` —— 安装进度页
- `Success` —— 完成页（含「立即启动」按钮）
- `Failure` —— 失败页（中文错误 + 日志链接）
- `Modify` —— 维护页（修复 / 卸载，二次运行已安装时显示）
- `Help` / `Loading` —— 帮助 / 检测页

向导流：**欢迎 → （点「自定义安装」）选择安装目录 → 安装进度 → 完成**。

顶部品牌区（Logo + 应用名 + 版本）在 `Window` 作用域内，所有页面共用、居中显示。

### 品牌与设计铁律

- 主色 **`#8B4AA6`**（浅色主题）。注意 ThmUtil 颜色是 **BGR 十六进制**，`#8B4AA6`
  写作 `A64A8B`（`#RRGGBB` → `BBGGRR`）。
- 按钮只用**圆角矩形**（radius 10px，由 PNG 贴图实现）。ThmUtil 本身没有圆角属性，
  圆角靠「图形按钮」（`ButtonImage`/`ButtonHoverImage`/`ButtonSelectedImage`）实现。
- **禁止椭圆 / 胶囊（Radius.Full/999）**、**禁止虚线**（未使用 `Static` 分隔线）。
- 所有用户可见文案、错误消息均为**简体中文**（见 `.wxl`）。

### 关键控件名（WixStdBA 按名字接线，不可改名）

`InstallButton`、`EulaRichedit`、`EulaAcceptCheckbox`、`OptionsButton`、
`InstallFolder`（编辑框，绑定 bundle 变量 `InstallFolder`）、`BrowseButton`、
`OptionsOkButton`、`OptionsCancelButton`、`OverallProgressPackageText`、
`OverallCalculatedProgressbar`、`OverallProgressText`、`ProgressCancelButton`、
`LaunchButton`、`RepairButton`、`UninstallButton`、`SuccessRestartButton`、
`FailureRestartButton`、`FailureLogFileLink`、`FailureMessageText`、`CheckingForUpdatesLabel`。

> 说明：`InstallButton` 必须留在 `Install`（欢迎）页上 —— WixStdBA 只在状态机位于
> 欢迎/已检测阶段时响应它；同时「安装目录」编辑框的值只会在**页面切换**时才写回变量，
> 所以目录选择沿用官方流程：`Install → Options → 确定(切回 Install，保存目录) → InstallButton`。

## 如何接入（在 `Bundle.wxs` 中，由后续会话完成）

本目录的文件**不会自动生效**，需要在
`installer/HanabePhotoManager.Setup/Bundle.wxs` 里把 `bal:WixStandardBootstrapperApplication`
指向这些文件。参考改动（**请勿改本目录以外的文件之外的内容，具体由负责 Bundle 的会话执行**）：

```xml
<BootstrapperApplication>
  <bal:WixStandardBootstrapperApplication
      Theme="rtfLicense"
      ThemeFile="themes\HanabeTheme.xml"
      LocalizationFile="themes\HanabeTheme.wxl"
      LicenseFile="..\HanabePhotoManager.Installer\license.rtf"
      LaunchTarget="[InstallFolder]HanabePhotoManager.App.exe"
      ShowVersion="yes" />
</BootstrapperApplication>
```

要点：

1. **`Theme="rtfLicense"` 保留**（schema 要求必填；`ThemeFile` 会覆盖其默认界面，
   而 `LicenseFile` 继续把 RTF 许可加载进 `EulaRichedit`）。
2. **删掉 `SuppressOptionsUI="yes"`**（否则「自定义安装 / 安装目录」页不显示）。
3. **`ThemeFile` / `LocalizationFile` 相对路径**相对 `.wixproj` 所在目录
   （`installer\HanabePhotoManager.Setup\`）。
4. **`LaunchTarget`** 指向安装后的主程序，`[InstallFolder]` 是 bundle 变量（见下）。
   设置后成功页才会出现「立即启动」按钮。
5. **让 `InstallFolder` 真正生效**（把用户选的目录传给 MSI，并给编辑框一个默认值）：

   ```xml
   <!-- 在 <Bundle> 内给编辑框一个默认值（与 MSI 的 INSTALLFOLDER 默认一致） -->
   <Variable Name="InstallFolder" Type="string" Value="[ProgramFiles64Folder]照片管理器" />

   <!-- 在 <MsiPackage> 内把目录传给 MSI -->
   <MsiPackage SourceFile="$(MsiPath)" ...>
     <MsiProperty Name="INSTALLFOLDER" Value="[InstallFolder]" />
   </MsiPackage>
   ```

   上面的默认目录 `[ProgramFiles64Folder]照片管理器` 与
   `Package.wxs` 里的 `<Directory Id="INSTALLFOLDER" Name="照片管理器" />` 保持一致，
   请以 MSI 实际目录为准核对。主题图片（`logo.png`、`button-*.png`、`progressbar.png`）
   由 Bal 扩展在编译主题时自动嵌入，**无需手动加为 payload**。

6. **⚠️ 必须排除 `.wxl` 的默认自动包含**。WiX SDK 默认把项目目录下所有 `**/*.wxl`
   当作 **bundle 级本地化**自动编译（`EmbeddedResource`）。而主题的 `.wxl` 只能通过
   `LocalizationFile` 作为 **BA 主题本地化**引用，不能被同时自动包含，否则会与内置主题
   本地化冲突，报 `WIX0100: The localization identifier 'X' has been duplicated`。
   接入时请在 `.wixproj` 里排除本文件（或整体关闭自动包含）：

   ```xml
   <!-- HanabePhotoManager.Setup.wixproj -->
   <ItemGroup>
     <!-- 主题 .wxl 只走 LocalizationFile，不要被 **/*.wxl 默认包含 -->
     <EmbeddedResource Remove="themes\HanabeTheme.wxl" />
   </ItemGroup>
   ```

   同样地，如果项目根目录还有其他 `.wxl`（例如 `Theme.zh-CN.wxl`）也是给主题用的，
   也需要一并排除，避免重复。

## 验证

- 主题 XML 与 `.wxl` 均为 UTF-8、格式正确（已用 XML 解析校验，loc 引用无缺失）。
- 图片均为带 alpha 的 PNG，尺寸与控件一致。
- 由于本外壳未接入 `Bundle.wxs`（本任务不允许改它），`dotnet build` 不会编译到本主题；
  主题要在接入 `ThemeFile` 后才会参与编译。
