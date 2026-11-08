using System.Collections.Concurrent;
using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Platform;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>
/// 照片墙缩略图按需加载：进入图库只预载前 N 张，其余由可见区域请求。
/// （之前一次解码整个图库几千张原图，4 线程跑满几分钟，界面发卡。）
/// </summary>
public class GalleryLazyThumbnailTests
{
    [Fact]
    public async Task PreviewPage_LoadsOnlyEagerThumbnails_ThenViewportOnDemand()
    {
        var root = Path.Combine(Path.GetTempPath(), "HanabeLazyThumbs", Guid.NewGuid().ToString("N"));
        var dateDir = Path.Combine(root, "11月", "11.07", "JPG直出");
        Directory.CreateDirectory(dateDir);
        var previous = PlatformServices.Thumbnails;
        var backend = new CountingBackend();
        try
        {
            PlatformServices.Thumbnails = new ThumbnailResolver(backend);
            int Mine() => backend.Paths.Keys.Count(path => path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
            var viewModel = new MainWindowViewModel();
            var files = new List<PreviewFileViewModel>();
            for (var i = 0; i < MainWindowViewModel.GalleryEagerThumbnailCount + 140; i++)
            {
                var path = Path.Combine(dateDir, $"T{i:0000}.jpg");
                File.WriteAllBytes(path, [(byte)(i % 250), 1]);
                var file = new PreviewFileViewModel(Path.GetFileName(path), "JPG直出", path, "1 KB", "JPG", null);
                files.Add(file);
                viewModel.PreviewFiles.Add(file);
            }
            viewModel.CurrentPreviewCategory = "视频";
            viewModel.CurrentPreviewCategory = "全部";

            viewModel.CurrentPage = "Preview";
            await WaitUntilAsync(() => Mine() >= MainWindowViewModel.GalleryEagerThumbnailCount);
            await Task.Delay(300);
            Mine().Should().Be(MainWindowViewModel.GalleryEagerThumbnailCount,
                "进入图库时只预载前一屏左右，不再解码整个图库");

            var tail = viewModel.VisiblePreviewFiles.Skip(MainWindowViewModel.GalleryEagerThumbnailCount + 100).Take(20).ToList();
            tail.Should().HaveCount(20);
            tail.Should().OnlyContain(item => item.Thumbnail == null);
            viewModel.RequestGalleryViewportThumbnails(tail);
            await WaitUntilAsync(() => tail.All(item => item.Thumbnail != null));
            tail.Should().OnlyContain(item => item.Thumbnail != null);
            Mine().Should().Be(MainWindowViewModel.GalleryEagerThumbnailCount + 20);
        }
        finally
        {
            PlatformServices.Thumbnails = previous;
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(50);
    }

    private sealed class CountingBackend : IThumbnailBackend
    {
        public ConcurrentDictionary<string, int> Paths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public object? Decode(string path, int width) { Paths.AddOrUpdate(path, 1, (_, n) => n + 1); return new object(); }
        public object? Shell(string path, int width, bool allowExtraction) => null;
    }
}
