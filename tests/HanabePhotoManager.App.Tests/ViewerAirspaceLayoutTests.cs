using System;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>
/// 查看器 HwndHost airspace 修复防回归（2026-08-14 用户反馈「视频功能栏被视频盖住，点不到」）。
/// 根因：LibVLCSharp VideoView 是 HwndHost（Win32 子窗口），渲染在一切 WPF 内容之上，
/// 浮动工具栏被全窗口视频窗口物理遮挡。修复 = 布局外置：视频播放且工具栏可见时给
/// VideoHost 预留顶/底工具栏带 + 右信息面板带（UpdateVideoHostLayout），视频窗口物理上
/// 不覆盖功能栏。以下断言锁住修复代码存在且挂在关键路径上，防止未来改布局回归。
/// </summary>
public class ViewerAirspaceLayoutTests
{
    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("未找到仓库根目录（HanabePhotoManager.sln）");
    }

    private static string ReadViewerCode() =>
        File.ReadAllText(Path.Combine(FindSourceRoot(), "src", "HanabePhotoManager.App", "PhotoViewerWindow.xaml.cs"));

    [Fact]
    public void VideoHost_HasAirspaceReserveConstants()
    {
        var code = ReadViewerCode();

        code.Should().Contain("UpdateVideoHostLayout");
        code.Should().Contain("VideoBarReserveTop");
        code.Should().Contain("VideoBarReserveBottom");
        code.Should().Contain("VideoBarReserveRight");
        // 预留带必须真正赋值到 VideoHost.Margin（布局外置的核心动作）
        code.Should().Contain("VideoHost.Margin != target");
        code.Should().Contain("VideoHost.Margin = target");
    }

    [Fact]
    public void VideoHostLayout_Refreshed_OnPlayStopAndOverlayToggle()
    {
        var code = ReadViewerCode();

        // 播放视频、停止视频、工具栏调出/隐藏 4 条关键路径 + 方法定义本身，
        // 任何一条回归（比如把工具栏挪回视频上层）都会导致调用次数减少
        var calls = Regex.Matches(code, "UpdateVideoHostLayout\\(\\)").Count;
        calls.Should().BeGreaterThanOrEqualTo(5, "视频播放/停止与工具栏显隐都必须刷新 VideoHost 布局");

        // 信息面板收起完成后释放右侧视频带
        code.Should().Contain("InfoPanel.Visibility = Visibility.Collapsed;");
        code.Should().Contain("UpdateVideoHostLayout();");
    }

    [Fact]
    public void VideoHost_StaysStretchAligned_SoMarginReserveApplies()
    {
        var xaml = File.ReadAllText(
            Path.Combine(FindSourceRoot(), "src", "HanabePhotoManager.App", "PhotoViewerWindow.xaml"));

        // VideoHost 必须保持 Stretch 拉伸对齐，Margin 预留带才能生效（不能改回 Auto/固定尺寸）
        xaml.Should().Contain("x:Name=\"VideoHost\"");
        xaml.Should().Contain("HorizontalAlignment=\"Stretch\"");
        xaml.Should().Contain("VerticalAlignment=\"Stretch\"");
    }
}
