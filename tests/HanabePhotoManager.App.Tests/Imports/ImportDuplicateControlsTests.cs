using System.IO;
using System.Xml.Linq;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests.Imports;

public sealed class ImportDuplicateControlsTests
{
    [Fact]
    public void NewSettings_EnableDuplicateProtection()
    {
        Assert.True(new AppSettings().CheckDuplicatesOnImport);
    }

    [Fact]
    public void ImportPage_DuplicateControlsReflectTheActualWorkflow()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            directory = directory.Parent;
        var document = XDocument.Load(Path.Combine(directory!.FullName, "src", "HanabePhotoManager.App", "MainWindow.xaml"));
        var check = document.Descendants().SingleOrDefault(element =>
            element.Name.LocalName == "CheckBox" &&
            (string?)element.Attribute("IsChecked") == "{Binding CheckDuplicatesOnImport, Mode=TwoWay}");
        Assert.NotNull(check);
        Assert.NotEqual("False", (string?)check.Attribute("IsEnabled"));
        foreach (var (label, property) in new[]
                 {
                     ("全部跳过（推荐）", "SkipImportDuplicates"),
                     ("全部仍导入", "KeepImportDuplicates"),
                     ("逐个选择", "ReviewImportDuplicates")
                 })
        {
            var radio = document.Descendants().Single(element =>
                element.Name.LocalName == "RadioButton" && (string?)element.Attribute("Content") == label);
            Assert.Equal($"{{Binding {property}, Mode=TwoWay}}", (string?)radio.Attribute("IsChecked"));
            Assert.NotEqual("False", (string?)radio.Attribute("IsEnabled"));
        }
    }
}
