using HanabePhotoManager.App.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests.Imports;

public sealed class ImportSourcePickerTests
{
    [Fact]
    public void PickerContract_ReturnsAReadOnlyFolderCollection()
    {
        IImportSourcePicker picker = new StubImportSourcePicker(["a", "b"]);

        Assert.Equal(new[] { "a", "b" }, picker.PickFolders(string.Empty));
    }

    private sealed class StubImportSourcePicker(IReadOnlyList<string> paths) : IImportSourcePicker
    {
        public IReadOnlyList<string> PickFolders(string initialDirectory) => paths;
    }
}
