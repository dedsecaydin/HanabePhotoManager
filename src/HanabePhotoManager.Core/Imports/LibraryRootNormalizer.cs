using System.IO;

namespace HanabePhotoManager.Core.Imports;

/// <summary>
/// 照片库根路径规范化。核心规则：根相对路径（如 "\Hanabe\拍照"，单反斜杠开头、无盘符）
/// 优先按"丢失反斜杠的 UNC 路径"识别——补双反斜杠成 "\\Hanabe\拍照"，若该 UNC 共享可访问
/// 则返回 UNC 格式，绝不 <see cref="Path.GetFullPath"/> 成当前盘符的绝对路径
/// （本机 C:\Hanabe\拍照 只是残留副本，不是真实照片库）。
/// </summary>
public static class LibraryRootNormalizer
{
    /// <summary>用真实文件系统探测目录可访问性。</summary>
    public static string? Normalize(string? path)
        => Normalize(path, directoryExists: null);

    /// <summary>
    /// 可注入目录探测器的规范化入口（测试用：注入固定返回值即可确定性验证 UNC 分支，
    /// 不依赖真实网络共享是否在线）。
    /// </summary>
    public static string? Normalize(string? path, Func<string, bool>? directoryExists)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        try
        {
            // 幂等：已完全限定（UNC \\server\share 或盘符 C:\...）的路径直接去尾部分隔符，
            // 不经过 GetFullPath，UNC 前缀原样保留。
            if (Path.IsPathFullyQualified(path))
            {
                return TrimRoot(path);
            }

            // 单反斜杠根相对路径（"\Hanabe\拍照"）：优先补双反斜杠成 UNC 候选 "\\Hanabe\拍照"。
            // 若该共享可访问则按 UNC 返回（GetFullPath 对 UNC 保留格式）——绝不转成 C 盘路径。
            if (path.Length >= 2 && path[0] == '\\' && path[1] != '\\')
            {
                var uncCandidate = @"\" + path;
                if (directoryExists?.Invoke(uncCandidate) ?? Directory.Exists(uncCandidate))
                {
                    return TrimRoot(Path.GetFullPath(uncCandidate));
                }
            }

            // 兜底：UNC 候选不可访问（或本来就是普通相对路径）时才 GetFullPath 成盘符绝对路径。
            return TrimRoot(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // 无法解析的非法路径原样返回，绝不让加载/保存流程崩溃。
            return path;
        }
    }

    private static string TrimRoot(string full)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(full);
        // 防误伤：TrimEndingDirectorySeparator("C:\") 会得到 "C:"（不再是完全限定路径），
        // 此时保留原样，避免把盘符根路径修坏。
        return Path.IsPathFullyQualified(trimmed) ? trimmed : full;
    }
}
