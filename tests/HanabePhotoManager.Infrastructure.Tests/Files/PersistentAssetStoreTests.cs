using FluentAssertions;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.Infrastructure.Tests.Files;

public sealed class PersistentAssetStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HanabeAssetTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Import_CopiesAssetIntoManagedDirectory()
    {
        var sourceDirectory = Path.Combine(_root, "source");
        var managedDirectory = Path.Combine(_root, "managed");
        Directory.CreateDirectory(sourceDirectory);
        var source = Path.Combine(sourceDirectory, "temporary-background.png");
        File.WriteAllText(source, "image-content");
        var store = new PersistentAssetStore(managedDirectory);

        var savedPath = store.Import(source, "background");
        File.Delete(source);

        savedPath.Should().Be(Path.Combine(managedDirectory, "background.png"));
        File.Exists(savedPath).Should().BeTrue();
        File.ReadAllText(savedPath).Should().Be("image-content");
    }

    [Fact]
    public void Import_ReplacesPreviousManagedAssetAtomically()
    {
        var sourceDirectory = Path.Combine(_root, "source");
        var managedDirectory = Path.Combine(_root, "managed");
        Directory.CreateDirectory(sourceDirectory);
        var first = Path.Combine(sourceDirectory, "first.jpg");
        var second = Path.Combine(sourceDirectory, "second.jpg");
        File.WriteAllText(first, "first");
        File.WriteAllText(second, "second");
        var store = new PersistentAssetStore(managedDirectory);

        store.Import(first, "avatar");
        var savedPath = store.Import(second, "avatar");

        File.ReadAllText(savedPath).Should().Be("second");
        Directory.GetFiles(managedDirectory, "*.tmp*").Should().BeEmpty();
    }

    [Fact]
    public void Find_RecoversPreviouslyImportedAsset()
    {
        var sourceDirectory = Path.Combine(_root, "source");
        var managedDirectory = Path.Combine(_root, "managed");
        Directory.CreateDirectory(sourceDirectory);
        var source = Path.Combine(sourceDirectory, "avatar.webp");
        File.WriteAllText(source, "avatar");
        var store = new PersistentAssetStore(managedDirectory);
        var savedPath = store.Import(source, "avatar");

        var recoveredPath = store.Find("avatar");

        recoveredPath.Should().Be(savedPath);
    }

    [Fact]
    public void Delete_RemovesManagedAsset()
    {
        var sourceDirectory = Path.Combine(_root, "source");
        var managedDirectory = Path.Combine(_root, "managed");
        Directory.CreateDirectory(sourceDirectory);
        var source = Path.Combine(sourceDirectory, "background.png");
        File.WriteAllText(source, "background");
        var store = new PersistentAssetStore(managedDirectory);
        store.Import(source, "background");

        store.Delete("background");

        store.Find("background").Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
