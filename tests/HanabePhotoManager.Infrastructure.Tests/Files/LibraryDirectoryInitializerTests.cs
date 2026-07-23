using FluentAssertions;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.Infrastructure.Tests.Files;

public sealed class LibraryDirectoryInitializerTests
{
    [Fact]
    public void EnsureDateTree_CreatesSixCategoryDirectoriesAndIsIdempotent()
    {
        using var workspace = new DirectoryWorkspace();
        var date = new LibraryDate(2026, 7, 11);
        var initializer = new LibraryDirectoryInitializer();

        initializer.EnsureDateTree(workspace.Root, date);
        initializer.EnsureDateTree(workspace.Root, date);

        foreach (var category in LibraryDirectoryInitializer.CategoryFolders)
        {
            Directory.Exists(Path.Combine(workspace.Root, date.RelativePath, category)).Should().BeTrue(category);
        }
    }

    [Fact]
    public void EnsureDateTree_RejectsRootEscapeWhenRootResolvesToFileParentSiblingPrefix()
    {
        using var workspace = new DirectoryWorkspace();
        var root = Path.Combine(workspace.Root, "library");
        Directory.CreateDirectory(root);
        var sibling = root + "-sibling";
        Directory.CreateDirectory(sibling);

        var initializer = new LibraryDirectoryInitializer();
        initializer.EnsureDateTree(root, new LibraryDate(2026, 7, 11));

        Directory.Exists(sibling).Should().BeTrue();
        Directory.EnumerateFileSystemEntries(sibling).Should().BeEmpty();
    }

    private sealed class DirectoryWorkspace : IDisposable
    {
        public DirectoryWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"hanabe-dirs-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
