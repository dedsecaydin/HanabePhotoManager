using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public class ImportCategoryPaletteTests
{
    [Theory]
    [InlineData("RAW", "#FFB9A2FF")]
    [InlineData("jpg", "#FFFF8FB8")]
    [InlineData("action 视频", "#FF6FB7FF")]
    [InlineData("视频", "#FFFFB36B")]
    [InlineData("修后", "#FF7EE0C3")]
    [InlineData("素材", "#FFC9C9D6")]
    [InlineData("其他", "#FF9A96A8")]
    public void ReturnsPortableColor(string category, string expected) =>
        Assert.Equal(expected, ImportCategoryPalette.For(category));

    [Fact]
    public void WpfBindingConvertsPortableColorToBrush()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var border = new System.Windows.Controls.Border();
                border.SetBinding(System.Windows.Controls.Border.BackgroundProperty,
                    new System.Windows.Data.Binding { Source = ImportCategoryPalette.For("RAW") });
                var brush = Assert.IsType<System.Windows.Media.SolidColorBrush>(border.Background);
                Assert.Equal(System.Windows.Media.Color.FromRgb(0xB9, 0xA2, 0xFF), brush.Color);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }
}
