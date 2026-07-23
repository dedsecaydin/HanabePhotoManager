using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class FaceSearchServiceTests
{
    [Fact]
    public async Task Reference_and_identical_library_photo_produce_a_match()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Assets", "face-reference.jpg");
        var library = Path.Combine(Path.GetTempPath(), $"hanabe-face-{Guid.NewGuid():N}");
        Directory.CreateDirectory(library);
        var libraryPhoto = Path.Combine(library, "JK0001.jpg");
        File.Copy(source, libraryPhoto);

        try
        {
            var service = new FaceSearchService();
            var reference = await service.CreateReferenceAsync(source, CancellationToken.None);
            var matches = await service.SearchAsync(
                reference, library, 0.42, progress: null, CancellationToken.None);

            reference.Embedding.Should().NotBeEmpty();
            matches.Should().ContainSingle();
            matches[0].Path.Should().Be(libraryPhoto);
            matches[0].Similarity.Should().BeGreaterThan(0.95);
        }
        finally
        {
            Directory.Delete(library, recursive: true);
        }
    }
}
