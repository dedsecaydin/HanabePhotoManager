using HanabePhotoManager.App.Platform;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public class PlatformImageFactoryTests
{
    [Fact]
    public void WindowsFactoryCreatesFrozenBgraImage()
    {
        IImageFactory factory = new WpfImageFactory();
        var image = Assert.IsAssignableFrom<BitmapSource>(factory.FromBgra32(1, 1, [0, 0, 255, 255]));
        Assert.True(image.IsFrozen);
        Assert.Equal(PixelFormats.Bgra32, image.Format);
        byte[] pixel = new byte[4];
        image.CopyPixels(pixel, 4, 0);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, pixel);
    }

    [Fact]
    public void WindowsPersonThumbnailPreservesExifRotation()
    {
        var source = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, new byte[8], 8);
        source.Freeze();
        var rotated = WpfImageFactory.ApplyExifOrientation(source, 6);
        Assert.Equal(1, rotated.PixelWidth);
        Assert.Equal(2, rotated.PixelHeight);
        Assert.True(rotated.IsFrozen);
    }

    [Fact]
    public void WindowsImageBindingConvertsPortableStretch()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var image = new System.Windows.Controls.Image();
                image.SetBinding(System.Windows.Controls.Image.StretchProperty, new System.Windows.Data.Binding { Source = "UniformToFill" });
                Assert.Equal(Stretch.UniformToFill, image.Stretch);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void WindowsBrushBindingConvertsPortableViewport()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var brush = new ImageBrush();
                System.Windows.Data.BindingOperations.SetBinding(brush, TileBrush.ViewportProperty, new System.Windows.Data.Binding { Source = "0,0,0.25,0.5" });
                Assert.Equal(new System.Windows.Rect(0, 0, 0.25, 0.5), brush.Viewport);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        Assert.Null(failure);
    }
}
