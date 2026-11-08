using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using HanabePhotoManager.Mac.Services;
using HanabePhotoManager.Mac.ViewModels;
using HanabePhotoManager.Mac.Views;

namespace HanabePhotoManager.Mac;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        HanabePhotoManager.App.Platform.PlatformServices.AppVersion = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0";
        HanabePhotoManager.App.Services.AppDataPaths.PlatformRoot = MacSettingsStore.SharedDataDirectory(MacSettingsStore.DataDirectory);
        HanabePhotoManager.App.Platform.PlatformServices.Dispatcher = new AvaloniaUiDispatcher();
        HanabePhotoManager.App.Platform.PlatformServices.Search = new MacSearchPlatform();
        HanabePhotoManager.App.Platform.PlatformServices.PlayWave = MacWavePlayer.Play;
        HanabePhotoManager.App.Platform.PlatformServices.Images = new AvaloniaImageFactory();
        HanabePhotoManager.App.Platform.PlatformServices.ReadClusterPixels = HanabePhotoManager.App.Platform.PortableClusterPixelReader.Read;
        HanabePhotoManager.App.Platform.PlatformServices.ImageHealthDecoder = new HanabePhotoManager.App.Services.PortableImageHealthDecoder();
        HanabePhotoManager.App.Platform.PlatformServices.StorageCapacity = new HanabePhotoManager.App.Platform.PortableStorageCapacityProvider();
        HanabePhotoManager.App.Platform.PlatformServices.PixelArtRenderer = new MacPixelArtRenderer();
        HanabePhotoManager.App.Services.FaceEmbeddingProvider.PlatformFactory = () =>
            HanabePhotoManager.App.Services.PortableFaceRecognitionFactory.Create(HanabePhotoManager.App.Services.FaceRecognitionRuntimeOptions.Current);
        HanabePhotoManager.App.Services.FaceSearchProvider.PlatformFactory = () => new HanabePhotoManager.App.Services.FaceSearchService(
            () => new HanabePhotoManager.App.Services.PortableFaceSearchEngine(HanabePhotoManager.App.Services.FaceEmbeddingProvider.Create()));
        HanabePhotoManager.App.Services.PhotoClassifierFactory.PlatformFactory = HanabePhotoManager.App.Services.PortablePhotoClassifierFactory.Create;
        HanabePhotoManager.App.Platform.PlatformServices.DecodeHeif = new MacHeifDecoder(Path.Combine(MacSettingsStore.DataDirectory, "heif-temp")).Decode;
        HanabePhotoManager.App.Platform.PlatformServices.OriginMetadata = new MacOriginMetadataStore();
        if (OperatingSystem.IsMacOS())
        {
            HanabePhotoManager.App.Platform.PlatformServices.CollageColorConverter = () => new MacCollageColorConverter("/System/Library/ColorSync/Profiles/sRGB Profile.icc");
            HanabePhotoManager.App.Platform.PlatformServices.Startup = new MacStartupRegistration(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "LaunchAgents", "com.hanabe.photomanager.plist"),
                Environment.ProcessPath ?? string.Empty);
            HanabePhotoManager.App.Platform.PlatformServices.Secrets = new MacKeychainSecretProtector(new NativeMacKeychainBackend());
            HanabePhotoManager.App.Platform.PlatformServices.RecycleBin = new MacTrashService();
        }
        HanabePhotoManager.App.Platform.PlatformServices.Thumbnails = new HanabePhotoManager.App.Services.ThumbnailResolver(new MacThumbnailBackend());
        HanabePhotoManager.App.Platform.PlatformServices.Dialogs = new AvaloniaDialogService();
        HanabePhotoManager.App.Platform.PlatformServices.ConfirmDelete = (message, groups, files) =>
            HanabePhotoManager.App.Platform.PlatformServices.Dialogs.ConfirmAsync("移入废纸篓", $"{message}\n\n共 {groups} 组、{files} 个文件。", dangerous: true);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = MacSettingsStore.Load();
            RequestedThemeVariant = settings.Theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
            var viewModel = new MainViewModel(settings);
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.ShutdownRequested += (_, _) => viewModel.Import.CancelRunning();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
