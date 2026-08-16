using FluentAssertions;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class NavigationOrderPolicyTests
{
    [Fact]
    public void MainNavigation_DoesNotExposeStandaloneSemanticSearch()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.NavigationItems.Select(item => item.Key).Should().NotContain("SemanticSearch");
    }

    [Fact]
    public void Normalize_RemovesUnknownAndDuplicateKeys_ThenAppendsMissingDefaults()
    {
        var result = NavigationOrderPolicy.Normalize(
            ["Preview", "Unknown", "Preview", "Home"],
            ["Home", "Import", "Preview"]);

        result.Should().Equal("Preview", "Home", "Import");
    }

    [Fact]
    public void Normalize_WhenStoredOrderIsMissing_ReturnsBuiltInOrder()
    {
        var result = NavigationOrderPolicy.Normalize(null, ["Home", "Import", "Preview"]);

        result.Should().Equal("Home", "Import", "Preview");
    }

    [Fact]
    public void NavigationDisplayMode_ExposesAllSupportedPresentations()
    {
        Enum.GetValues<NavigationDisplayMode>().Should().Equal(
            NavigationDisplayMode.Text,
            NavigationDisplayMode.Icon,
            NavigationDisplayMode.IconAndText);
    }

    [Fact]
    public void MoveNavigationItem_ReordersStableDestinationKeys()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.MoveNavigationItem("Preview", "Home");

        viewModel.NavigationItems.Select(item => item.Key)
            .Should().StartWith("Preview", "Home", "Import");
        viewModel.NavigationItems.Select(item => item.Order)
            .Should().Equal(Enumerable.Range(0, viewModel.NavigationItems.Count));
    }

    [Fact]
    public void MoveNavigationItem_WhenDroppedBelowMidpoint_InsertsAfterTarget()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.MoveNavigationItem("Home", "Preview", insertAfter: true);

        viewModel.NavigationItems.Select(item => item.Key)
            .Should().StartWith("Import", "Preview", "Home");
    }
}
