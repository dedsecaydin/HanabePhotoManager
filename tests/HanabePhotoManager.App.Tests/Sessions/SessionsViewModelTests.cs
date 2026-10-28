using System.IO;
using Xunit;
using HanabePhotoManager.Core.Sessions;
using HanabePhotoManager.Infrastructure.Sessions;
using HanabePhotoManager.App.Sessions;
namespace HanabePhotoManager.App.Tests.Sessions;
[CollectionDefinition("VNext WPF application", DisableParallelization = true)]
public sealed class VNextWpfApplicationCollection { }

[Collection("VNext WPF application")]
public class SessionsViewModelTests
{
    private sealed class Reader : ICaptureTimeReader { public CaptureMember Read(string path) => new(path, path, new DateTime(2026, 1, 1).AddMinutes(path == "b" ? 31 : 0), TimeSpan.Zero, "A"); }
    [Fact]
    public async Task DetectionPreservesUnrelatedSavedGroupsAndSortNeverRedetects()
    {
        var dir = Path.Combine(@"D:\HanabePhoto\.artifacts\sessions-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { var store = new JsonSessionStore(Path.Combine(dir, "data.json")); await store.CommitAsync(0, [new("old", "Earlier", [new("old", "old.jpg", null, null, "A")])], new()); var vm = new SessionsViewModel(store, new Reader(), ["b", "a"]); await vm.InitializeAsync(); await vm.DetectCommand.ExecuteAsync(null); Assert.Equal(3, vm.Groups.Count); var ids = vm.Groups.Select(g => g.Id).ToArray(); vm.Sort = "文件名"; Assert.Equal(ids, vm.Groups.Select(g => g.Id)); await vm.CommitCommand.ExecuteAsync(null); var restarted = new SessionsViewModel(new JsonSessionStore(Path.Combine(dir, "data.json")), new Reader(), []); await restarted.InitializeAsync(); Assert.Equal(3, restarted.Groups.Count); await restarted.UndoCommand.ExecuteAsync(null); Assert.Single(restarted.Groups); Assert.Equal("old", restarted.Groups[0].Id); }
        finally { Directory.Delete(dir, true); }
    }
    [Fact] public async Task InvalidThresholdDoesNotReadOrPersist() { var dir = Path.Combine(@"D:\HanabePhoto\.artifacts\sessions-tests", Guid.NewGuid().ToString("N")); var path = Path.Combine(dir, "data.json"); var vm = new SessionsViewModel(new JsonSessionStore(path), new Reader(), ["a"]) { ThresholdMinutes = double.NaN }; await vm.DetectCommand.ExecuteAsync(null); Assert.Empty(vm.Groups); Assert.False(File.Exists(path)); }
    [Fact]
    public async Task LargePreviewPublishesCollectionsOnceAndThrottlesProgress()
    {
        var vm = new SessionsViewModel(new JsonSessionStore(@"D:\HanabePhoto\.artifacts\sessions-tests\no-preview-writes.json"),
            new Reader(), Enumerable.Range(0, 5000).Select(index => index.ToString("D5")));
        var progressChanges = 0;
        var groupsChanges = 0;
        var memberChanges = 0;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.Progress)) Interlocked.Increment(ref progressChanges);
            if (args.PropertyName == nameof(vm.Groups)) Interlocked.Increment(ref groupsChanges);
            if (args.PropertyName == nameof(vm.Members)) Interlocked.Increment(ref memberChanges);
        };
        await vm.DetectCommand.ExecuteAsync(null);
        Assert.Single(vm.Groups);
        Assert.Equal(5000, vm.Members.Count);
        Assert.Equal(1, groupsChanges);
        Assert.Equal(1, memberChanges);
        Assert.InRange(progressChanges, 1, 101);
        Assert.Equal(100, vm.Progress);
    }
    [Fact]
    public void WindowInitializesAndLaysOutInBothThemes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            System.Windows.Application? application = null;
            try
            {
                application = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    application.Resources.MergedDictionaries.Clear();
                    var colors = new System.Windows.ResourceDictionary { Source = new Uri($"pack://application:,,,/HanabePhotoManager.App;component/Themes/Colors/Colors.Violet.{theme}.xaml") };
                    application.Resources.MergedDictionaries.Add(colors);
                    foreach (var tokenPath in new[] { "Tokens/Sizing.xaml", "Tokens/Spacing.xaml", "Tokens/Radius.xaml", "Tokens/Shadows.xaml", "Typography/FontFamilies.xaml", "Typography/TypeScale.xaml" }) application.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = new Uri("pack://application:,,,/HanabePhotoManager.App;component/Themes/" + tokenPath) });
                    application.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = new Uri($"pack://application:,,,/HanabePhotoManager.App;component/Themes/Themes/Violet.{theme}.xaml") });
                    var vm = new SessionsViewModel(new JsonSessionStore(@"D:\HanabePhoto\.artifacts\sessions-tests\ui-never-written.json"), new Reader(), []);
                    var window = new SessionsWindow(vm);
                    try { var content = (System.Windows.FrameworkElement)window.Content; content.Measure(new System.Windows.Size(1080, 720)); content.Arrange(new System.Windows.Rect(0, 0, 1080, 720)); content.UpdateLayout(); Assert.Same(vm, window.DataContext); Assert.True(content.ActualWidth > 0); Capture(content, theme, content.Parent?.GetType().Name ?? "Tool"); }
                    finally { window.Close(); }
                    foreach (var toolWindow in new System.Windows.Window[] { new HanabePhotoManager.App.Collage.CollageWindow(), new HanabePhotoManager.App.Video.SLog3Window() })
                    {
                        try { var content = (System.Windows.FrameworkElement)toolWindow.Content; content.Measure(new System.Windows.Size(1080, 720)); content.Arrange(new System.Windows.Rect(0, 0, 1080, 720)); content.UpdateLayout(); Assert.True(content.ActualWidth > 0); Capture(content, theme, content.Parent?.GetType().Name ?? "Tool"); }
                        finally { toolWindow.Close(); }
                        VerifyTraditionalCollageEntry();
                }
                }
            }
            catch (Exception e) { failure = e; }
            finally { application?.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(20))); Assert.Null(failure);
    }
    private static void VerifyTraditionalCollageEntry()
    {
        var viewModel = new HanabePhotoManager.App.ViewModels.CompressionViewModel();
        var page = new HanabePhotoManager.App.Compression.CompressionPage { DataContext = viewModel };
        try
        {
            var traditional = LogicalDescendants(page).OfType<System.Windows.Controls.Button>()
                .Single(button => button.Tag as string == "Collage");
            Assert.Single(LogicalDescendants(page).OfType<System.Windows.Controls.Button>(),
                button => button.Tag as string == "IntelligentCollage");
            var grid = Assert.IsAssignableFrom<System.Windows.FrameworkElement>(page.FindName("ToolGridHost"));
            var detail = Assert.IsAssignableFrom<System.Windows.FrameworkElement>(page.FindName("ToolDetailHost"));
            Assert.Equal(System.Windows.Visibility.Visible, grid.Visibility);
            Assert.Equal(System.Windows.Visibility.Collapsed, detail.Visibility);

            traditional.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, traditional));

            Assert.True(viewModel.IsCollageMode);
            Assert.Equal(HanabePhotoManager.App.ViewModels.ImageToolMode.Collage, viewModel.SelectedToolMode);
            Assert.Equal(System.Windows.Visibility.Collapsed, grid.Visibility);
            Assert.Equal(System.Windows.Visibility.Visible, detail.Visibility);
            Assert.Equal(2, viewModel.CollageOrientations.Count);
            Assert.False(viewModel.CollageLimitOutputSize);
            page.Measure(new System.Windows.Size(1080, 720));
            page.Arrange(new System.Windows.Rect(0, 0, 1080, 720));
            page.UpdateLayout();
        }
        finally { page.DataContext = null; }
    }
    private static IEnumerable<System.Windows.DependencyObject> LogicalDescendants(System.Windows.DependencyObject parent)
    {
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(parent).OfType<System.Windows.DependencyObject>())
        {
            yield return child;
            foreach (var descendant in LogicalDescendants(child)) yield return descendant;
        }
    }    private static void Capture(System.Windows.FrameworkElement content, string theme, string name)
    {
        var root = @"D:\HanabePhoto\.artifacts\vnext-20261026\rendered-ui";
        Directory.CreateDirectory(root);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1080, 720, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(root, name + "-" + theme + ".png"));
        encoder.Save(stream);
    }
}


