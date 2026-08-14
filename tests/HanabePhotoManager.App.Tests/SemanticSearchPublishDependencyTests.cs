using FluentAssertions;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class SemanticSearchPublishDependencyTests
{
    [Fact]
    public void InfrastructureProject_DeclaresTensorRuntimeRequiredByPublishedClipInference()
    {
        var root = FindSourceRoot();
        var project = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HanabePhotoManager.Infrastructure",
            "HanabePhotoManager.Infrastructure.csproj"));

        project.Should().Contain("<PackageReference Include=\"System.Numerics.Tensors\" Version=\"9.0.0\" />");
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
