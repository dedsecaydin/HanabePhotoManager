namespace HanabePhotoManager.App.Platform;

public static class PlatformServices
{
    public static IUiDispatcher Dispatcher { get; set; } = new InlineUiDispatcher();
    public static IImageFactory Images { get; set; } = new NullImageFactory();
    public static Func<string, BgraImagePixels?> ReadClusterPixels { get; set; } = _ => null;
    public static Func<string, CancellationToken, SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>> DecodeHeif { get; set; } =
        (_, _) => throw new PlatformNotSupportedException("此平台尚未配置 HEIF 解码器。");
    public static HanabePhotoManager.App.Services.ThumbnailResolver Thumbnails { get; set; } = new(new UnavailableThumbnailBackend());
    public static IDialogService Dialogs { get; set; } = new NullDialogService();
    public static IDuplicateDialogs DuplicateDialogs { get; set; } = new NullDuplicateDialogs();
    public static ISecretProtector Secrets { get; set; } = new UnavailableSecretProtector();
    public static string AppVersion { get; set; } = "0";
    public static IStorageCapacityProvider StorageCapacity { get; set; } = new UnavailableStorageCapacityProvider();
    public static HanabePhotoManager.App.PixelArt.IPixelArtRenderer PixelArtRenderer { get; set; } = new HanabePhotoManager.App.PixelArt.UnavailablePixelArtRenderer();
    public static ISearchPlatform Search { get; set; } = new NullSearchPlatform();
    public static Action<byte[]> PlayWave { get; set; } = _ => { };
    public static Action AnimatePhotoWallLayout { get; set; } = () => { };
    public static Func<IReadOnlyList<HanabePhotoManager.App.ViewModels.PersonAlbumItemViewModel>, HanabePhotoManager.App.ViewModels.PersonAlbumItemViewModel?> PickMergeTarget { get; set; } = _ => null;
    public static Func<bool> ConfirmImportMove { get; set; } = () => false;
    public static Func<string, bool> ConfirmImportRenames { get; set; } = _ => false;
    public static Func<string, bool> ShowQuarantine { get; set; } = _ => false;
    /// <summary>「按场次拆分到文件夹」的确认窗口（可改新文件夹备注）；为 null 时用普通确认框。</summary>
    public static Func<HanabePhotoManager.App.Browsing.DaySplitPreview, bool>? ConfirmDaySplit { get; set; }
    public static Func<string, int, int, Task<bool>> ConfirmDelete { get; set; } = (_, _, _) => Task.FromResult(false);
    public static HanabePhotoManager.App.Services.IRecycleBinFileService RecycleBin { get; set; } = new HanabePhotoManager.App.Services.UnavailableRecycleBinFileService();
    public static HanabePhotoManager.App.Imports.IImportSourcePicker ImportSourcePicker { get; set; } = new UnavailableImportSourcePicker();
    public static HanabePhotoManager.App.Services.IStartupRegistrationService Startup { get; set; } = new UnavailableStartupRegistration();
    public static HanabePhotoManager.App.Services.IWindowsWallpaperService Wallpaper { get; set; } = new UnavailableWallpaperService();
    public static HanabePhotoManager.App.Services.IImageHealthDecoder ImageHealthDecoder { get; set; } = new HanabePhotoManager.App.Services.UnavailableImageHealthDecoder();
    internal static HanabePhotoManager.App.Imports.IFileOriginMetadataStore OriginMetadata { get; set; } = new UnavailableOriginMetadataStore();
    internal static HanabePhotoManager.App.Recovery.IRecoveryPlatform Recovery { get; set; } = new HanabePhotoManager.App.Recovery.UnavailableRecoveryPlatform();
    public static Func<HanabePhotoManager.App.Collage.ICollageColorConverter> CollageColorConverter { get; set; } = () => new HanabePhotoManager.App.Collage.UnmanagedCollageColorConverter();
}
