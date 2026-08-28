using System.IO;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using FluentAssertions;
using HanabePhotoManager.App.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportNamingPresetTests
{
    [Fact]
    public void ImportNamingTemplate_CustomTemplate_IncludesTheCurrentPresetInTheSelector()
    {
        const string template = "{date}_{orig}";
        var viewModel = new MainWindowViewModel();

        viewModel.ImportNamingTemplate = template;

        viewModel.ImportNamingPresets.Should().Contain(viewModel.SelectedImportNamingPreset);
        viewModel.SelectedImportNamingPreset.Kind.Should().Be(ImportNamingPresetKind.Custom);
    }

    [Fact]
    public async Task InitializeAsync_CustomTemplate_UpdatesPresetStateAndRaisesBindingNotifications()
    {
        const string template = "{date}_{orig}";
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-import-preset-init-" + Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("HANABE_APP_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("HANABE_APP_DATA_DIR", directory);
            await new AppSettingsStore().SaveAsync(new AppSettings { ImportNamingTemplate = template });

            var viewModel = new MainWindowViewModel();
            var notifications = new List<string?>();
            viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

            await viewModel.InitializeAsync();

            viewModel.ImportNamingTemplate.Should().Be(template);
            viewModel.SelectedImportNamingPreset.Kind.Should().Be(ImportNamingPresetKind.Custom);
            viewModel.ImportNamingPresets.Should().Contain(viewModel.SelectedImportNamingPreset);
            notifications.Should().Contain(nameof(MainWindowViewModel.ImportNamingTemplate));
            notifications.Should().Contain(nameof(MainWindowViewModel.SelectedImportNamingPreset));
            notifications.Should().Contain(nameof(MainWindowViewModel.ImportNamingPresets));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HANABE_APP_DATA_DIR", previousDirectory);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("JK{seq}", ImportNamingPresetKind.Sequence)]
    [InlineData("{orig}", ImportNamingPresetKind.Original)]
    [InlineData("JK{seq}（{orig}）", ImportNamingPresetKind.SequenceAndOriginal)]
    [InlineData("{date}_{orig}", ImportNamingPresetKind.Custom)]
    public void Resolve_MapsKnownTemplatesAndPreservesCustom(string template, ImportNamingPresetKind expected)
    {
        ImportNamingPreset.Resolve(template).Kind.Should().Be(expected);
    }
}
