# 自定义文件夹（虚拟相册）模块划分

## 用户可见结果

用户可从任意磁盘位置添加文件夹，在应用内的“自定义相册”中浏览其中的图片。相册显示名可改，移除操作只删除应用保存的引用，绝不修改磁盘文件夹或文件。

| 模块 | 职责 | 文件路径 | 依赖 |
|---|---|---|---|
| Core 合约与模型 | 定义虚拟相册及持久化接口 | `src/HanabePhotoManager.Core/Albums/*` | 无 |
| Infrastructure 存储 | 以原子 JSON 文件读写相册引用 | `src/HanabePhotoManager.Infrastructure/Albums/*` | Core、System.IO |
| App 浏览服务 | 枚举已有相册目录中的可显示图片，不删除或移动源文件 | `src/HanabePhotoManager.App/Albums/*` | Core、WPF Imaging |
| App 表现层 | 相册列表、重命名、移除、选择文件夹和图片墙 | `src/HanabePhotoManager.App/Albums/*`、`MainWindow.*` | App 浏览服务、现有导航令牌 |
| 测试 | 模型、JSON 持久化、图像枚举和 ViewModel 行为 | `tests/*/Albums/*` | 对应生产层 |

## 接口契约

- `CustomAlbum(Guid Id, string DisplayName, string FolderPath)`：只保存虚拟引用；`FolderPath` 为规范化绝对路径。
- `ICustomAlbumStore.LoadAsync(CancellationToken)`：读取全部相册。
- `ICustomAlbumStore.SaveAsync(IReadOnlyCollection<CustomAlbum>, CancellationToken)`：原子替换持久化清单。
- `CustomAlbumPhotoScanner.ScanAsync(string folderPath, CancellationToken)`：只枚举支持的图片文件；目录不存在时返回空结果并由 UI 说明。
- `CustomAlbumsViewModel.AddAsync / RenameSelectedAsync / RemoveSelectedAsync / OpenAsync`：分别对应添加、仅改显示名、仅移除引用、浏览。

## 关键规则

- 可添加不同路径；同一路径不重复创建引用。
- 浏览递归扫描所选目录及子目录的图像，完全只读。
- 相册数据保存在 `%LocalAppData%\\HanabePhotoManager\\custom-albums.json`，不进入 OneDrive、源代码或照片目录。
- UI 复用 `Navigation.Item`、`Card.*`、`Button.*`、`StatusPanel.*` 与语义 Brush；不添加页面级颜色或样式。

## 实施顺序

1. Core 模型与接口 → 2. Infrastructure JSON 实现 → 3. App 扫描/缩略图与 ViewModel → 4. 导航和 WPF 页面 → 5. 单元测试、全量验证、发布。
