using HanabePhotoManager.App.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests.Imports;

public sealed class ImportSourcePickerTests
{
    [Fact]
    public void PickerContract_ReturnsAReadOnlyPathCollection()
    {
        IImportSourcePicker picker = new StubImportSourcePicker(["a.jpg", "b.jpg"]);

        Assert.Equal(new[] { "a.jpg", "b.jpg" }, picker.PickFiles(string.Empty));
    }

    private sealed class StubImportSourcePicker(IReadOnlyList<string> paths) : IImportSourcePicker
    {
        public IReadOnlyList<string> PickFiles(string initialDirectory) => paths;
    }
}
