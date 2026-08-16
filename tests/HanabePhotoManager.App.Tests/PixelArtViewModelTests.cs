using FluentAssertions;
using HanabePhotoManager.App.PixelArt;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PixelArtViewModelTests
{
    [Fact]
    public void Defaults_ToPreset128()
    {
        var viewModel = new PixelArtViewModel();

        viewModel.SelectedSize.Should().Be(128);
        viewModel.IsCustom.Should().BeFalse();
        viewModel.CustomSizeText.Should().Be("128");
        viewModel.ResolveEffectiveSize().Should().Be(128);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public void SelectPreset_UsesPresetAndExitsCustomMode(int size)
    {
        var viewModel = new PixelArtViewModel();
        viewModel.SelectCustom();
        viewModel.CustomSizeText = "96";

        viewModel.SelectPreset(size);

        viewModel.SelectedSize.Should().Be(size);
        viewModel.IsCustom.Should().BeFalse();
        viewModel.CustomSizeText.Should().Be(size.ToString());
        viewModel.ResolveEffectiveSize().Should().Be(size);
    }

    [Theory]
    [InlineData("96", 96)]
    [InlineData("512", 512)]
    [InlineData("5000", 4096)]
    public void SelectCustom_ResolvesValidCustomSize(string text, int expected)
    {
        var viewModel = new PixelArtViewModel();
        viewModel.SelectCustom();
        viewModel.CustomSizeText = text;

        viewModel.IsCustom.Should().BeTrue();
        viewModel.ResolveEffectiveSize().Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void SelectCustom_InvalidInputFallsBackTo128(string text)
    {
        var viewModel = new PixelArtViewModel();
        viewModel.SelectCustom();
        viewModel.CustomSizeText = text;

        viewModel.ResolveEffectiveSize().Should().Be(128);
    }
}
