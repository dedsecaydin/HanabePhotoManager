using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public class SharedImportModelTests
{
    [Fact]
    public void PortableImportCoordinatorLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.Mac.Services.MacImportEngine).Assembly.GetName().Name);
    }

    [Fact]
    public void ImportPresentationModelsAreInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(ImportPreviewItemViewModel).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(ImportDateGraphGroupViewModel).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(CategoryChoice).Assembly.GetName().Name);
        Assert.Equal(typeof(object), typeof(ImportPreviewItemViewModel).GetProperty("Thumbnail")!.PropertyType);
    }

    [Fact]
    public void ViewerModelIsPortable()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(PhotoViewerViewModel).Assembly.GetName().Name);
        Assert.Equal(typeof(object), typeof(PhotoViewerViewModel).GetProperty("Image")!.PropertyType);
    }

    [Fact]
    public void AgentProtocolLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Agents.LocalMcpServer).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Agents.OpenAiCompatibleAgent).Assembly.GetName().Name);
    }

    [Fact]
    public void SemanticSearchModelsLiveInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Search.SemanticSearchViewModel).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Search.SearchResultItemViewModel).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Search.SemanticBrowseRanking).Assembly.GetName().Name);
    }

    [Fact]
    public void CompressionAndCollageLogicLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Compression.ImageCollageService).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(CompressionViewModel).Assembly.GetName().Name);
    }

    [Fact]
    public void PhotoAnalysisOrchestrationLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(PhotoAnalysisViewModel).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Services.PhotoClassifierFactory).Assembly.GetName().Name);
    }

    [Fact]
    public void HeifConversionWorkflowLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Imports.HeifJpegConverter).Assembly.GetName().Name);
    }

    [Fact]
    public void SoundSchedulingLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Services.HanabeSoundService).Assembly.GetName().Name);
    }

    [Fact]
    public void PeopleAndCollageOrchestrationLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Services.PeopleAlbumService).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Collage.CollageViewModel).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Services.ILocalFaceEmbeddingService).Assembly.GetName().Name);
    }

    [Fact]
    public void ApplicationSettingsLogicLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Services.AppSettingsStore).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(BrowseDisplayMode).Assembly.GetName().Name);
    }

    [Fact]
    public void FaceSearchPresentationLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(FaceSearchViewModel).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Services.FaceSearchMatch).Assembly.GetName().Name);
    }

    [Fact]
    public void PeoplePresentationLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(PeopleAlbumViewModel).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(PersonPhotoViewModel).Assembly.GetName().Name);
    }

    [Fact]
    public void GalleryItemModelIsPortable()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(PreviewFileViewModel).Assembly.GetName().Name);
        Assert.Equal(typeof(object), typeof(PreviewFileViewModel).GetProperty("Thumbnail")!.PropertyType);
    }

    [Fact]
    public void BackgroundAndIconPropertiesUsePortableTypes()
    {
        Assert.Equal(typeof(object), typeof(MainWindowViewModel).GetProperty("BackgroundImage")!.PropertyType);
        Assert.Equal(typeof(object), typeof(MainWindowViewModel).GetProperty("AppIconImage")!.PropertyType);
        Assert.Equal(typeof(string), typeof(MainWindowViewModel).GetProperty("BackgroundImageStretch")!.PropertyType);
    }

    [Fact]
    public void PlatformServiceContractsLiveInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Services.IStartupRegistrationService).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Services.IWindowsWallpaperService).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Imports.IImportSourcePicker).Assembly.GetName().Name);
    }

    [Fact]
    public void TreemapPresentationModelsLiveInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Browsing.Treemap.ProgressiveTreemapViewModel).Assembly.GetName().Name);
        Assert.Equal(typeof(object), typeof(HanabePhotoManager.App.Browsing.Treemap.TreemapItemViewModel).GetProperty("Thumbnail")!.PropertyType);
    }

    [Fact]
    public void WatermarkPresentationLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Watermark.WatermarkViewModel).Assembly.GetName().Name);
        Assert.Equal(typeof(object), typeof(HanabePhotoManager.App.Watermark.WatermarkViewModel).GetProperty("PreviewImage")!.PropertyType);
        Assert.Equal(typeof(string), typeof(HanabePhotoManager.App.Watermark.WatermarkViewModel).GetProperty("PreviewTileViewport")!.PropertyType);
    }

    [Fact]
    public void RecoveryPresentationLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Recovery.RecoveryViewModel).Assembly.GetName().Name);
        Assert.Equal(typeof(object), typeof(HanabePhotoManager.App.Recovery.RecoveryViewModel).GetProperty("PreviewImage")!.PropertyType);
    }

    [Fact]
    public void RecognitionRuntimeOptionsLiveInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Services.FaceRecognitionRuntimeOptions).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Services.MobileClipRuntimeOptions).Assembly.GetName().Name);
    }

    [Fact]
    public void GallerySectionContractLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Controls.IWallSectionHeader).Assembly.GetName().Name);
    }

    [Fact]
    public void MainWindowOrchestrationLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(MainWindowViewModel).Assembly.GetName().Name);
        Assert.Equal("HanabePhotoManager.Shared", typeof(LibraryDateNode).Assembly.GetName().Name);
    }

    [Fact]
    public void ImageSplitPresentationLivesInSharedAssembly()
    {
        Assert.Equal("HanabePhotoManager.Shared", typeof(HanabePhotoManager.App.Splitting.ImageSplitViewModel).Assembly.GetName().Name);
        Assert.Equal(typeof(object), typeof(HanabePhotoManager.App.Splitting.ImageSplitViewModel).GetProperty("Preview")!.PropertyType);
    }

    [Fact]
    public void ShootingChartMarginUsesPortableValue()
    {
        Assert.Equal(typeof(string), typeof(MainWindowViewModel).GetProperty("ShootingBarMargin")!.PropertyType);
        var converted = (System.Windows.Thickness)System.ComponentModel.TypeDescriptor.GetConverter(typeof(System.Windows.Thickness)).ConvertFromInvariantString("1,0,1,0")!;
        Assert.Equal(new System.Windows.Thickness(1, 0, 1, 0), converted);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var element = new System.Windows.Controls.Border();
                element.SetBinding(System.Windows.FrameworkElement.MarginProperty, new System.Windows.Data.Binding { Source = "1,0,1,0" });
                Assert.Equal(new System.Windows.Thickness(1, 0, 1, 0), element.Margin);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        Assert.Null(failure);
    }

}
