using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HanabePhotoManager.App.Splitting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImageSplitWindowTests
{
    [Fact]
    public void LoadedWindowRendersAndExportsInBothThemes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    _ = System.IO.Packaging.PackUriHelper.Create(new Uri("http://localhost"));
                    string root = @"D:\HanabePhoto\.artifacts\split-ui";
                    Directory.CreateDirectory(root);
                    var source = Path.Combine(root, "fixture.png");
                    using (var image = new Image<Rgba32>(601, 401))
                    {
                        for (int y = 0; y < image.Height; y++)
                            for (int x = 0; x < image.Width; x++) image[x, y] = new Rgba32((byte)(x * 255 / 601), (byte)(y * 255 / 401), 150);
                        await image.SaveAsPngAsync(source);
                    }
                    foreach (var theme in new[] { "Light", "Dark" })
                    {
                        var resources = LoadTheme(theme);
                        var window = new ImageSplitWindow(resources);
                        try
                        {
                            var vm = Assert.IsType<ImageSplitViewModel>(window.DataContext);
                            await vm.LoadAsync(source);
                            Assert.NotNull(vm.Preview); Assert.True(vm.CanExport);
                            vm.Scale = 75; vm.PositionX = 100; vm.PositionY = 0;
                            Assert.Equal(0, vm.Crop.Y); Assert.Equal(vm.ImageWidth, vm.Crop.Right);
                            foreach (var mode in vm.Modes)
                            {
                                vm.Mode = mode;
                                Assert.Equal(mode.Columns * mode.Rows, ImageSplitService.Plan(vm.Crop, mode.Columns, mode.Rows).Count);
                            }
                            foreach (var size in new[] { new System.Windows.Size(1140, 680), new System.Windows.Size(860, 520) })
                            {
                                var content = (FrameworkElement)window.Content;
                                content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout();
                                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                                bitmap.Render(content);
                                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                                using var stream = File.Create(Path.Combine(root, $"{theme}-{size.Width}.png")); encoder.Save(stream);
                            }
                            vm.OutputDirectory = root;
                            await vm.ExportCommand.ExecuteAsync(null);
                            Assert.True(vm.HasResult, vm.Status); Assert.Equal(9, Directory.GetFiles(vm.ResultDirectory).Length);
                            vm.Png = false;
                            await vm.ExportCommand.ExecuteAsync(null);
                            Assert.Equal(9, Directory.GetFiles(vm.ResultDirectory, "*.jpg").Length);
                            await vm.LoadAsync(Path.Combine(root, "missing.jpg"));
                            Assert.False(vm.IsBusy); Assert.Contains("载入失败", vm.Status);
                        }
                        finally { window.Close(); }
                    }
                }
                catch (Exception ex) { failure = ex; }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45))); Assert.Null(failure);
    }
    private static ResourceDictionary LoadTheme(string theme)
    {
        // Flatten sibling dictionaries for a stand-alone window without an Application singleton.
        var root = new System.Xml.Linq.XElement("{http://schemas.microsoft.com/winfx/2006/xaml/presentation}ResourceDictionary");
        void Append(string path)
        {
            var document = System.Xml.Linq.XDocument.Load(path).Root!;
            foreach (var element in document.Elements())
            {
                if (element.Name.LocalName == "ResourceDictionary.MergedDictionaries")
                {
                    foreach (var merged in element.Elements()) Append(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, merged.Attribute("Source")!.Value)));
                }
                else
                {
                    var copy = new System.Xml.Linq.XElement(element);
                    foreach (var attribute in document.Attributes().Where(a => a.IsNamespaceDeclaration))
                        if (copy.Attribute(attribute.Name) is null) copy.Add(new System.Xml.Linq.XAttribute(attribute));
                    foreach (var uri in copy.DescendantsAndSelf().SelectMany(e => e.Attributes("UriSource")))
                        {
                            uri.Value = uri.Value.Replace("pack://application:,,,/Assets/", "pack://application:,,,/HanabePhotoManager.App;component/Assets/");
                        }
                        root.Add(copy);
                }
            }
        }
        Append($@"D:\HanabePhoto\src\HanabePhotoManager.App\Themes\Themes\Violet.{theme}.xaml");
        return (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(root.ToString());
    }}






