using System.Security.Cryptography;
using System.Security;
using FluentAssertions;
using HanabePhotoManager.Core.Cloud;
using HanabePhotoManager.Infrastructure.Cloud;

namespace HanabePhotoManager.Infrastructure.Tests.Cloud;

public sealed class SimulatedCloudProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hanabe-simulated-" + Guid.NewGuid().ToString("N"));
    private readonly string _remote;

    public SimulatedCloudProviderTests()
    {
        _remote = Path.Combine(_root, "remote");
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task UploadListAndVerify_RoundTripsFile()
    {
        var provider = new SimulatedCloudProvider(_remote, 10_000_000);
        var source = Path.Combine(_root, "source.jpg");
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);

        var remoteId = await provider.UploadAsync(source, new CloudPath("/Hanabe照片备份/7月/07.14/source.jpg"), null, default);
        var listed = await CollectAsync(provider.ListAsync(new CloudPath("/Hanabe照片备份/7月/07.14"), default));
        var verified = await provider.VerifyAsync(remoteId,
            new CloudTransferFile(source, new CloudRelativePath("7月/07.14/source.jpg"), 4, null), default);

        remoteId.Should().Be("/Hanabe照片备份/7月/07.14/source.jpg");
        listed.Should().ContainSingle(item => item.Name == "source.jpg" && item.Kind == CloudObjectKind.Image);
        verified.IsVerified.Should().BeTrue();
        verified.RemoteId.Should().Be(remoteId);

        var restarted = new SimulatedCloudProvider(_remote, 10_000_000);
        (await restarted.VerifyAsync(remoteId,
            new CloudTransferFile(source, new CloudRelativePath("7月/07.14/source.jpg"), 4, null), default))
            .IsVerified.Should().BeFalse("an unknown pre-existing remote id must not accept an arbitrary prefix");
    }

    [Fact]
    public async Task List_ReturnsDirectChildrenFoldersFirstAndStableOrder()
    {
        var provider = new SimulatedCloudProvider(_remote, 10_000_000);
        var directory = Path.Combine(_remote, "photos");
        Directory.CreateDirectory(Path.Combine(directory, "B"));
        Directory.CreateDirectory(Path.Combine(directory, "a"));
        await File.WriteAllTextAsync(Path.Combine(directory, "z.MP4"), "video");
        await File.WriteAllTextAsync(Path.Combine(directory, "A.JPG"), "image");
        await File.WriteAllTextAsync(Path.Combine(directory, "nested.txt"), "text");
        await File.WriteAllTextAsync(Path.Combine(directory, "B", "deep.jpg"), "deep");
        await File.WriteAllTextAsync(Path.Combine(directory, ".hanabe-upload-test.tmp"), "incomplete");
        await File.WriteAllTextAsync(Path.Combine(directory, ".cloud-index.lock"), "locked");

        var items = await CollectAsync(provider.ListAsync(new CloudPath("/photos"), default));

        items.Select(i => i.Name).Should().Equal("a", "B", "A.JPG", "nested.txt", "z.MP4");
        items[0].Kind.Should().Be(CloudObjectKind.Folder);
        items[1].Kind.Should().Be(CloudObjectKind.Folder);
        items.Should().NotContain(i => i.Name == "deep.jpg");
        items.Should().NotContain(i => i.Name == ".hanabe-upload-test.tmp");
        items.Should().NotContain(i => i.Name == ".cloud-index.lock");
        items.Single(i => i.Name == "A.JPG").ThumbnailKey.Should().NotBeNull();
        items.Single(i => i.Name == "z.MP4").Kind.Should().Be(CloudObjectKind.Video);
    }

    [Fact]
    public async Task PathTraversalAndAbsoluteCloudPath_CannotEscapeRemoteRoot()
    {
        var provider = new SimulatedCloudProvider(_remote, 10_000_000);
        var source = Path.Combine(_root, "source.bin");
        await File.WriteAllBytesAsync(source, [1]);

        Func<Task> act = async () => { await provider.UploadAsync(source, new CloudPath("/safe/file.bin"), null, default); };
        await act.Should().NotThrowAsync();
        var outside = Path.Combine(_root, "outside.bin");
        File.Exists(outside).Should().BeFalse();
        (await CollectAsync(provider.ListAsync(new CloudPath("/safe"), default)))
            .Should().ContainSingle(item => item.Name == "file.bin");

        Action traversal = () => new CloudPath("/safe/../outside");
        traversal.Should().Throw<ArgumentException>();
        Action drive = () => new CloudPath("/C:/outside");
        drive.Should().Throw<ArgumentException>();

        var external = Path.Combine(_root, "external");
        Directory.CreateDirectory(external);
        var link = Path.Combine(_remote, "link");
        try
        {
            Directory.CreateSymbolicLink(link, external);
            Func<Task> listWithLink = async () => await CollectAsync(provider.ListAsync(new CloudPath("/"), default));
            await listWithLink.Should().ThrowAsync<SecurityException>();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            // Windows test runners without symlink privileges still cover the lexical checks above.
        }
    }

    [Fact]
    public async Task Upload_ReportsCumulativeProgress_AndHonorsCancellationWithoutReplacingTarget()
    {
        var provider = new SimulatedCloudProvider(_remote, 10_000_000);
        var source = Path.Combine(_root, "source.bin");
        await File.WriteAllBytesAsync(source, new byte[2 * 1024 * 1024 + 7]);
        var destination = new CloudPath("/target.bin");
        await provider.UploadAsync(source, destination, null, default);
        var before = await File.ReadAllBytesAsync(Path.Combine(_remote, "target.bin"));
        IProgress<CloudUploadProgress> progress = new Progress<CloudUploadProgress>();
        using var cancellation = new CancellationTokenSource();
        var updates = new List<CloudUploadProgress>();
        progress = new InlineProgress<CloudUploadProgress>(update =>
        {
            updates.Add(update);
            if (update.BytesTransferred >= 1024 * 1024) cancellation.Cancel();
        });

        Func<Task> act = async () => { await provider.UploadAsync(source, destination, progress, cancellation.Token); };
        await act.Should().ThrowAsync<OperationCanceledException>();
        (await File.ReadAllBytesAsync(Path.Combine(_remote, "target.bin"))).Should().Equal(before);
        Directory.GetFiles(_remote, ".hanabe-upload-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        updates.Should().NotContain(p => p.BytesTransferred < 0 || p.BytesTransferred > p.TotalBytes);
    }

    [Fact]
    public async Task ThumbnailAndRead_AreCallerOwned_AndOnlyImagesHaveThumbnail()
    {
        var provider = new SimulatedCloudProvider(_remote, 10_000_000);
        var image = Path.Combine(_root, "photo.JPG");
        var video = Path.Combine(_root, "movie.mp4");
        await File.WriteAllBytesAsync(image, [10, 20]);
        await File.WriteAllBytesAsync(video, [30, 40]);
        var imageId = await provider.UploadAsync(image, new CloudPath("/photo.JPG"), null, default);
        var videoId = await provider.UploadAsync(video, new CloudPath("/movie.mp4"), null, default);
        var imageObject = (await CollectAsync(provider.ListAsync(new CloudPath("/"), default))).Single(i => i.RemoteId == imageId);
        var videoObject = (await CollectAsync(provider.ListAsync(new CloudPath("/"), default))).Single(i => i.RemoteId == videoId);

        await using var thumbnail = await provider.OpenThumbnailAsync(imageObject, default);
        thumbnail.Should().NotBeNull();
        using var reader = new MemoryStream();
        await thumbnail!.CopyToAsync(reader);
        reader.ToArray().Should().Equal([10, 20]);
        (await provider.OpenThumbnailAsync(videoObject, default)).Should().BeNull();
        await using var content = await provider.OpenReadAsync(imageObject, default);
        content.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task List_AllowsOpeningEachImageInsideAwaitForeachWithoutDeadlock()
    {
        var provider = new SimulatedCloudProvider(_remote, 10_000_000);
        var source = Path.Combine(_root, "inside-loop.jpg");
        await File.WriteAllBytesAsync(source, [6, 7]);
        await provider.UploadAsync(source, new CloudPath("/inside-loop.jpg"), null, default);
        var opened = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var item in provider.ListAsync(new CloudPath("/"), timeout.Token))
        {
            await using var thumbnail = await provider.OpenThumbnailAsync(item, timeout.Token);
            if (thumbnail is not null) opened++;
        }

        opened.Should().Be(1);
    }

    [Fact]
    public async Task Verify_ChecksSizeAndSha256()
    {
        var provider = new SimulatedCloudProvider(_remote, 10_000_000);
        var source = Path.Combine(_root, "photo.png");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(source, bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var id = await provider.UploadAsync(source, new CloudPath("/photo.png"), null, default);
        var expected = new CloudTransferFile(source, new CloudRelativePath("photo.png"), bytes.Length, hash);
        (await provider.VerifyAsync(id, expected, default)).IsVerified.Should().BeTrue();
        var wrongSize = new CloudTransferFile(source, new CloudRelativePath("photo.png"), 99, hash);
        var wrongHash = new CloudTransferFile(source, new CloudRelativePath("photo.png"), bytes.Length, new string('0', 64));
        (await provider.VerifyAsync(id, wrongSize, default)).IsVerified.Should().BeFalse();
        (await provider.VerifyAsync(id, wrongHash, default)).IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_DoesNotAcceptSameContentFromAnotherUploadedDirectory()
    {
        var provider = new SimulatedCloudProvider(_remote, 10_000_000);
        var sourceA = Path.Combine(_root, "source-a", "same.bin");
        var sourceB = Path.Combine(_root, "source-b", "same.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceA)!);
        Directory.CreateDirectory(Path.GetDirectoryName(sourceB)!);
        await File.WriteAllBytesAsync(sourceA, [8, 9, 10]);
        await File.WriteAllBytesAsync(sourceB, [8, 9, 10]);
        var idA = await provider.UploadAsync(sourceA, new CloudPath("/A/same.bin"), null, default);
        var idB = await provider.UploadAsync(sourceB, new CloudPath("/B/same.bin"), null, default);
        var expectedA = new CloudTransferFile(sourceA, new CloudRelativePath("same.bin"), 3, null);

        (await provider.VerifyAsync(idA, expectedA, default)).IsVerified.Should().BeTrue();
        (await provider.VerifyAsync(idB, expectedA, default)).IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_WindowsRemoteIdCaseVariantCannotBypassKnownUploadSourceBinding()
    {
        if (!OperatingSystem.IsWindows()) return;
        var provider = new SimulatedCloudProvider(_remote, 10_000_000);
        var original = Path.Combine(_root, "original", "Test.jpg");
        var other = Path.Combine(_root, "other", "Test.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(original)!);
        Directory.CreateDirectory(Path.GetDirectoryName(other)!);
        await File.WriteAllBytesAsync(original, [4, 5, 6]);
        await File.WriteAllBytesAsync(other, [4, 5, 6]);
        await provider.UploadAsync(original, new CloudPath("/Photos/Test.jpg"), null, default);
        var caseVariantId = "/photos/test.jpg";
        var correct = new CloudTransferFile(original, new CloudRelativePath("photos/test.jpg"), 3, null);
        var wrongSource = new CloudTransferFile(other, new CloudRelativePath("photos/test.jpg"), 3, null);

        (await provider.VerifyAsync(caseVariantId, correct, default)).IsVerified.Should().BeTrue();
        (await provider.VerifyAsync(caseVariantId, wrongSource, default)).IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task AccountState_ReportsConfiguredCapacityAndUsedBytes()
    {
        var provider = new SimulatedCloudProvider(_remote, 100);
        var source = Path.Combine(_root, "a.bin");
        await File.WriteAllBytesAsync(source, new byte[7]);
        await provider.UploadAsync(source, new CloudPath("/a.bin"), null, default);

        var state = await provider.GetAccountStateAsync(default);

        state.Provider.Should().Be(CloudProviderKind.Simulated);
        state.IsAuthenticated.Should().BeTrue();
        state.TotalBytes.Should().Be(100);
        state.UsedBytes.Should().Be(7);
    }

    [Fact]
    public async Task AccountState_CleansCrashLeftUploadTemporaryFilesBeforeCapacityCheck()
    {
        Directory.CreateDirectory(Path.Combine(_remote, "nested"));
        var abandoned = Path.Combine(_remote, "nested", ".hanabe-upload-0123456789abcdef0123456789abcdef.tmp");
        await File.WriteAllBytesAsync(abandoned, new byte[90]);
        var userTemp = Path.Combine(_remote, "nested", ".user.tmp");
        var userLock = Path.Combine(_remote, "nested", ".cloud-index.lock");
        await File.WriteAllBytesAsync(userTemp, [2]);
        await File.WriteAllBytesAsync(userLock, [3]);
        var provider = new SimulatedCloudProvider(_remote, 10);
        var source = Path.Combine(_root, "one.bin");
        await File.WriteAllBytesAsync(source, [1]);

        await provider.UploadAsync(source, new CloudPath("/one.bin"), null, default);

        File.Exists(abandoned).Should().BeFalse();
        (await provider.GetAccountStateAsync(default)).UsedBytes.Should().Be(3);
        File.Exists(userTemp).Should().BeTrue();
        File.Exists(userLock).Should().BeTrue();
    }

    [Fact]
    public async Task AccountState_CountsProviderTemporaryWhenDeletionFails()
    {
        var abandoned = Path.Combine(_remote, ".hanabe-upload-0123456789abcdef0123456789abcdef.tmp");
        Directory.CreateDirectory(_remote);
        await File.WriteAllBytesAsync(abandoned, new byte[7]);
        await using var held = new FileStream(abandoned, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var provider = new SimulatedCloudProvider(_remote, 100);

        var used = (await provider.GetAccountStateAsync(default)).UsedBytes;
        used.Should().Be(OperatingSystem.IsWindows() ? 7 : 0);
    }

    [Fact]
    public async Task EnsureFolder_CreatesAndReturnsFolderObject()
    {
        var provider = new SimulatedCloudProvider(_remote, 100);
        var item = await provider.EnsureFolderAsync(new CloudPath("/Hanabe照片备份/7月"), default);
        item.Kind.Should().Be(CloudObjectKind.Folder);
        item.Path.Value.Should().Be("/Hanabe照片备份/7月");
        Directory.Exists(Path.Combine(_remote, "Hanabe照片备份", "7月")).Should().BeTrue();
    }

    private static async Task<IReadOnlyList<CloudObject>> CollectAsync(IAsyncEnumerable<CloudObject> source)
    {
        var items = new List<CloudObject>();
        await foreach (var item in source) items.Add(item);
        return items;
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
