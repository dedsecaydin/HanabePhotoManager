using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PeopleAlbumViewModelTests
{
    [Fact]
    public void ToggleBubbles_OpensAndClearSelectionCloses()
    {
        var store = Path.Combine(Path.GetTempPath(), $"people-{Guid.NewGuid():N}.json");
        var viewModel = new PeopleAlbumViewModel(new PeopleAlbumService(store), () => []);

        viewModel.ToggleBubblesCommand.Execute(null);
        viewModel.AreBubblesOpen.Should().BeTrue();
        viewModel.ClearSelectionCommand.Execute(null);
        viewModel.AreBubblesOpen.Should().BeFalse();
    }
}
