# 夸克网盘集成到 hanabephoto — 任务 Spec（子 agent 2）

## 项目铁律（必须遵守）
1. 项目：D:\hanabephoto（.NET 8 / C# 12 / WPF，HanabePhotoManager.sln，0.3.0-alpha）
2. 构建：`cd /d/hanabephoto && dotnet build HanabePhotoManager.sln -c Debug /warnaserror`（WarningsAsErrors，任何警告=失败）
3. 测试：`dotnet test HanabePhotoManager.sln -c Debug --no-build > log 2>&1` 再 grep 结果行（管道 tail 会 SIGPIPE 假 abort）
4. UI 颜色/圆角/阴影必须用设计 Token（DynamicResource Brush.* / Radius.* / Typography.*），禁止硬编码
5. 单文件不超 600 行（新文件可以，别往 MainWindowViewModel.cs 7000 行里加）
6. 改完必须构建 + 全量测试验证
7. 不提交 git，保留工作区改动，完成后报告改动文件列表
8. 同一仓库只允许你这个 agent 在改（子 agent 1 已完成才轮到你），不要和其他 agent 并行

## 夸克 CLI 工具（已安装，可调用）
- 位置：`C:\Users\fulia\AppData\Local\Hermes Agent CN Desktop\data\hermes-home\skills\quarkclouddrive\scripts\quark-drive.cjs`
- 运行：`node <路径>/quark-drive.cjs <command> [options]`
- 所有输出是 NDJSON 流：每行 `{"code":0,"msg":"...","action":"...","type":"result|list|progress","data":{...}}`，code=0 成功，负数失败（-103 未登录）
- 关键命令：
  - `get-user-info` → 账户状态（data 里有 userInfo 等）
  - `browse` → 目录浏览（列出文件，需要看具体参数；help 里没列出但内部有，试试 `browse --help`，不行就用 `search --keyword ""`）
  - `search --keyword <kw>` → 搜索
  - `create-folder --dir-path <path>` → 建目录
  - `upload <localPath> [--parent-fid <fid>]` → 上传
  - `read-file --fid <fid>` → 下载到本地（保存到 $OPENCLAW_RUNTIME_DIR/.quarkclouddrive）
  - `share <fids...>` / `saveas` / `move` / `download` 等
- 认证：未登录时运行命令返回 code=-103；`login` 命令启动浏览器 OAuth（阻塞等待）
- 配置存储：CLI 自己的 config（accounts）——不要直接读 token，调用 CLI 即可

## 当前 hanabephoto 云架构（已存在，别破坏）
- `src/HanabePhotoManager.Core/Cloud/ICloudProvider.cs` — 接口：Kind / GetAccountStateAsync / ListAsync / OpenThumbnailAsync / OpenReadAsync / EnsureFolderAsync / UploadAsync / VerifyAsync
- `src/HanabePhotoManager.Infrastructure/Cloud/BaiduCloudProvider.cs` — 百度实现（HTTP API，参考写法）
- `src/HanabePhotoManager.Infrastructure/Cloud/SimulatedCloudProvider.cs` — 本地模拟实现
- `src/HanabePhotoManager.App/Cloud/CloudHubViewModel.cs` — 右侧总览 VM（AccountTitle/AccountState/Items/TransferJobs 等），构造函数接收 ICloudProvider
- `src/HanabePhotoManager.App/Cloud/CloudPage.xaml.cs` — WebView2 内嵌浏览器页；`CreateCloudHubViewModelAsync` 创建 VM；百度 host 用 EncryptedCloudSessionStore 加载 token，夸克 host 目前用 UnauthenticatedCloudProvider（显示"未接入"）
- `src/HanabePhotoManager.App/Cloud/UnauthenticatedCloudProvider.cs` — 未接入占位
- `src/HanabePhotoManager.App/MainWindow.xaml` 2129-2155 行：CloudPageContainer 含 BaiduCloudPageHost + QuarkCloudPageHost（InitialUrl="https://pan.quark.cn"）
- `src/HanabePhotoManager.Core/Cloud/CloudModels.cs` — CloudProviderKind / CloudAccountState / CloudObject / CloudPath 等

## 任务：实现 QuarkCloudProvider 并接入夸克页

### 1. 新建 `src/HanabePhotoManager.Infrastructure/Cloud/QuarkCloudProvider.cs`
实现 ICloudProvider，底层调用 quark-drive.cjs（Process.Start node 执行，解析 NDJSON 流）：
- Kind => CloudProviderKind.Quark（确认枚举里有 Quark；没有就加）
- GetAccountStateAsync => 执行 `get-user-info`；code=0 返回已登录状态（DisplayName="夸克网盘"，StatusText="已连接"）；code=-103 或其他 => 返回未登录状态（IsAuthenticated=false，StatusText 如实"未登录/未授权"）
- ListAsync => 执行 `browse`（先试 `browse --help` 确认参数；若 browse 不可用，用 `search --keyword ""` 或能列目录的命令），把输出 data 映射成 CloudObject（区分目录/文件、大小、时间）
- OpenThumbnailAsync => 返回 null（夸克 CLI 没有直接缩略图接口，如实不支持）
- OpenReadAsync => 执行 `read-file --fid <fid>` 拿到本地文件路径，返回 FileStream（用完 dispose）
- EnsureFolderAsync => 执行 `create-folder --dir-path <path>` 返回 CloudObject
- UploadAsync => 执行 `upload <localPath>`（可选 --parent-fid），解析上传结果
- VerifyAsync => 尽力而为：返回成功（CLI 上传已确认），或按文件存在性判断
- 所有 CLI 调用：超时保护（如 60s）、stderr 收集、进程退出码检查；**任何异常/未登录都返回结构化结果，不抛致命异常**

### 2. 接入 CloudPage（src/HanabePhotoManager.App/Cloud/CloudPage.xaml.cs）
- 在 CreateCloudHubViewModelAsync（约 199 行）里：当 IsQuarkHost 为 true 时，创建 QuarkCloudProvider（注入 node 路径/CLI 路径）替代 UnauthenticatedCloudProvider
- 保持百度路径不变（BaiduCloudProvider + EncryptedCloudSessionStore）
- 确保夸克页打开时右侧总览显示"夸克网盘"（未登录则显示"未登录"提示，而不是"未接入"占位）

### 3. 设置页（可选增强，若时间允许）
- SettingsCenterPage 或网盘页内加一个"登录夸克网盘"按钮：点击后执行 `quark-drive.cjs login`（异步，浏览器 OAuth），完成后刷新账户状态
- 若复杂度高可只实现 provider + 状态显示，登录按钮放 CloudPage 状态面板

## 验收标准
1. dotnet build 0 警告 0 错误
2. 全量测试通过（记录通过数）
3. QuarkCloudProvider.cs 存在，GetAccountStateAsync 在未登录时返回"夸克网盘/未登录"而不是抛异常
4. 夸克页右侧总览不再显示"未接入"占位（显示真实夸克状态）
5. 百度网盘路径不受影响（构建+测试验证）

完成后用中文报告：新建/修改了哪些文件、QuarkCloudProvider 怎么实现 CLI 调用、构建测试结果、夸克页现在的显示状态。
