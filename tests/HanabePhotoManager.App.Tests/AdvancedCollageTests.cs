using FluentAssertions;
using HanabePhotoManager.App.Compression;
using HanabePhotoManager.Core.Collage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using System.IO;
namespace HanabePhotoManager.App.Tests;
public sealed class AdvancedCollageTests : IDisposable
{
    private readonly string root = Path.Combine(@"D:\HanabePhoto\.artifacts\collage-tests", Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData(CollageFileFormat.Jpeg)]
    [InlineData(CollageFileFormat.Png)]
    [InlineData(CollageFileFormat.Tiff)]
    public async Task ExportAllFormatsIsDecodableAndDoesNotOverwrite(CollageFileFormat format)
    {
        Directory.CreateDirectory(root); var source = Path.Combine(root, "source.png");
        using (var image = new Image<Rgba32>(80, 40, Color.Red)) await image.SaveAsPngAsync(source);
        var hash = await File.ReadAllBytesAsync(source);
        var photos = await ImageCollageService.ProbeAsync([source], CancellationToken.None);
        var plan = CollagePlanner.Candidates(photos, new(128, 128, Margin: 8, Gap: 4))[0];
        var service = new ImageCollageService();
        var first = await service.ComposeAsync(plan, new(root, format), null, CancellationToken.None);
        var second = await service.ComposeAsync(plan, new(root, format), null, CancellationToken.None);
        first.IsSuccess.Should().BeTrue(first.Message); second.IsSuccess.Should().BeTrue(); first.OutputPath.Should().NotBe(second.OutputPath);
        using var output = await Image.LoadAsync<Rgba32>(first.OutputPath!); output.Width.Should().Be(128); output.Height.Should().Be(128);
        output[64, 64].R.Should().BeGreaterThan(200); output[64, 10].G.Should().BeGreaterThan(220);
        (await File.ReadAllBytesAsync(source)).Should().Equal(hash);
    }
    [Fact]
    public async Task CancelNeverPublishes()
    {
        Directory.CreateDirectory(root); using var c = new CancellationTokenSource(); c.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ImageCollageService().ComposeAsync(new CollagePlan(100, 100, [new("missing", 0, 0, 100, 100)]), new(root), null, c.Token));
        Directory.GetFiles(root).Should().BeEmpty();
    }
    [Fact]
    public async Task TaggedAndUntaggedSourcesNormalizeToSrgbWithProfileAndNoTemporaryFiles()
    {
        Directory.CreateDirectory(root);
        var profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "drivers", "color", "sRGB Color Space Profile.icm");
        Assert.True(File.Exists(profilePath), "Windows sRGB fixture must be installed for this WIC verification.");
        string tagged = Path.Combine(root, "tagged.png"), untagged = Path.Combine(root, "untagged.png");
        using (var image = new Image<Rgba32>(40, 40, Color.Red))
        {
            await image.SaveAsPngAsync(untagged);
            image.Metadata.IccProfile = new SixLabors.ImageSharp.Metadata.Profiles.Icc.IccProfile(await File.ReadAllBytesAsync(profilePath));
            await image.SaveAsPngAsync(tagged);
        }
        var plan = new CollagePlan(80, 40, [new(tagged, 0, 0, 40, 40), new(untagged, 40, 0, 40, 40)]);
        var result = await new ImageCollageService().ComposeAsync(plan, new(root, CollageFileFormat.Png), null, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Message);
        using var output = await Image.LoadAsync<Rgba32>(result.OutputPath!);
        output.Metadata.IccProfile.Should().NotBeNull();
        output[20, 20].R.Should().BeGreaterThan(245); output[60, 20].R.Should().BeGreaterThan(245);
        Math.Abs(output[20, 20].G - output[60, 20].G).Should().BeLessThan(4);
        Directory.GetFiles(root, "*.icc", SearchOption.AllDirectories).Should().BeEmpty();
    }
    [Fact]
    public async Task DifferentRgbProfilesAreActuallyConvertedInsteadOfMerelyCopied()
    {
        Directory.CreateDirectory(root);
        var systemProfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "drivers", "color", "sRGB Color Space Profile.icm");
        var bytes = await File.ReadAllBytesAsync(systemProfile);
        static uint U32(byte[] b, int at) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(at, 4));
        int tags = (int)U32(bytes, 128);
        for (int i = 0; i < tags; i++)
        {
            int tag = 132 + i * 12; string name = System.Text.Encoding.ASCII.GetString(bytes, tag, 4);
            if (name is not ("rTRC" or "gTRC" or "bTRC")) continue;
            int offset = (int)U32(bytes, tag + 4); Assert.Equal("curv", System.Text.Encoding.ASCII.GetString(bytes, offset, 4)); int count = (int)U32(bytes, offset + 8); Assert.True(count > 1);
            // A synthetic linear-light RGB fixture shares sRGB primaries but replaces its transfer curves.
            for (int j = 0; j < count; j++) System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 12 + j * 2, 2), (ushort)(j * 65535L / (count - 1)));
        }
        // Windows recognizes standard profiles by their identifying header/description; this is a distinct synthetic space.
        Array.Clear(bytes, 48, 8); Array.Clear(bytes, 84, 16);
        for (int i = 0; i < bytes.Length - 4; i++) if ((bytes[i] == (byte)'s' || bytes[i] == (byte)'S') && bytes[i + 1] == (byte)'R' && bytes[i + 2] == (byte)'G' && bytes[i + 3] == (byte)'B') { bytes[i] = (byte)'L'; bytes[i + 1] = (byte)'I'; bytes[i + 2] = (byte)'N'; bytes[i + 3] = (byte)'R'; }
        string linear = Path.Combine(root, "linear.png"), srgb = Path.Combine(root, "srgb.png");
        using (var image = new Image<Rgba32>(20, 20, new Rgba32(128, 128, 128)))
        {
            image.Metadata.IccProfile = new SixLabors.ImageSharp.Metadata.Profiles.Icc.IccProfile(bytes); await image.SaveAsPngAsync(linear);
            image.Metadata.IccProfile = new SixLabors.ImageSharp.Metadata.Profiles.Icc.IccProfile(await File.ReadAllBytesAsync(systemProfile)); await image.SaveAsPngAsync(srgb);
        }
        var result = await new ImageCollageService().ComposeAsync(new CollagePlan(40, 20, [new(linear, 0, 0, 20, 20), new(srgb, 20, 0, 20, 20)]), new(root, CollageFileFormat.Png), null, CancellationToken.None);
        using var output = await Image.LoadAsync<Rgba32>(result.OutputPath!);
        output[10, 10].R.Should().BeInRange(183, 193); output[30, 10].R.Should().BeInRange(125, 131);
        output.Metadata.IccProfile.Should().NotBeNull();
    }
    [Fact]
    public async Task ExplicitFocalCropSelectsSubjectSideAndPreservesAspect()
    {
        Directory.CreateDirectory(root); string path = Path.Combine(root, "subject.png");
        using (var image = new Image<Rgba32>(100, 40, Color.Red))
        {
            for (int y = 0; y < 40; y++) for (int x = 60; x < 100; x++) image[x, y] = new Rgba32(0, 0, 255);
            await image.SaveAsPngAsync(path);
        }
        var result = await new ImageCollageService().ComposeAsync(new CollagePlan(40, 40, [new(path, 0, 0, 40, 40, .9, .5)]), new(root, CollageFileFormat.Png, Crop: true), null, CancellationToken.None);
        using var output = await Image.LoadAsync<Rgba32>(result.OutputPath!); output[20, 20].B.Should().BeGreaterThan(240); output[20, 20].R.Should().BeLessThan(10);
    }
    [Fact]
    public async Task FaceHintsUseExistingDetectorAndFailBackHonestly()
    {
        var photos = new[] { new CollagePhoto("one", 100, 100), new CollagePhoto("two", 100, 100) };
        var plans = CollagePlanner.Candidates(photos, new(200, 200, Margin: 4, Gap: 2));
        var hints = await new HanabePhotoManager.App.Collage.CollageSubjectHints(new FakeFaces()).ApplyAsync(plans, photos, CancellationToken.None);
        hints.Plans[0].Slots[0].FocusX.Should().BeApproximately(.8, .001);
        hints.Plans[0].Slots[1].FocusX.Should().Be(.5);
        hints.Message.Should().Contain("失败 1");
        hints.Plans[0].Slots.Select(s => s.Id).Should().Equal("one", "two");
    }
    [Fact]
    public void OrderLockPreventsMoveSwapAndNameSort()
    {
        var vm = new HanabePhotoManager.App.Collage.CollageViewModel(); vm.Photos.Add("z"); vm.Photos.Add("a"); vm.OrderLocked = true;
        vm.Move(0, 1); vm.Swap(0, 1); vm.Sort(false); vm.Photos.Should().Equal("z", "a");
        vm.OrderLocked = false; vm.Swap(0, 1); vm.Photos.Should().Equal("a", "z");
    }
    private sealed class FakeFaces : HanabePhotoManager.App.Services.ILocalFaceEmbeddingService
    {
        public Task<IReadOnlyList<HanabePhotoManager.App.Services.DetectedFace>> DetectAsync(string path, CancellationToken token)
            => path == "one" ? Task.FromResult<IReadOnlyList<HanabePhotoManager.App.Services.DetectedFace>>([new(path, [], 70, 10, 20, 20)]) : throw new InvalidOperationException("fixture detector failure");
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}




