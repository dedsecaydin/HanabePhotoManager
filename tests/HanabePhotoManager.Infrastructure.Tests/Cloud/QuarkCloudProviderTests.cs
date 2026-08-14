using System.IO;
using System.Text;
using FluentAssertions;
using HanabePhotoManager.Core.Cloud;
using HanabePhotoManager.Infrastructure.Cloud;

namespace HanabePhotoManager.Infrastructure.Tests.Cloud;

/// <summary>
/// Exercises QuarkCloudProvider against a fake quark-drive.cjs script that
/// emits canned NDJSON, so parsing/mapping logic is verified without needing a
/// real logged-in Quark account.
/// </summary>
public sealed class QuarkCloudProviderTests
{
    private static readonly Lazy<string> FakeCliPath = new(CreateFakeCli);

    [Fact]
    public async Task GetAccountState_WhenCliReportsAuthenticated_ReturnsConnectedStateWithCapacity()
    {
        using var scope = FakeMode("authed");
        var provider = CreateProvider();

        var state = await provider.GetAccountStateAsync(CancellationToken.None);

        state.IsAuthenticated.Should().BeTrue();
        state.Provider.Should().Be(CloudProviderKind.Quark);
        state.DisplayName.Should().Be("夸克网盘");
        state.UsedBytes.Should().Be(1024);
        state.TotalBytes.Should().Be(10240);
        state.StatusText.Should().Contain("已连接");
    }

    [Fact]
    public async Task GetAccountState_WhenCliReportsNotLoggedIn_ReturnsUnauthenticatedState()
    {
        using var scope = FakeMode("unauth");
        var provider = CreateProvider();

        var state = await provider.GetAccountStateAsync(CancellationToken.None);

        state.IsAuthenticated.Should().BeFalse();
        state.DisplayName.Should().Be("夸克网盘");
        state.UsedBytes.Should().Be(0);
        state.TotalBytes.Should().Be(0);
        state.StatusText.Should().Contain("未登录");
    }

    [Fact]
    public async Task GetAccountState_WhenCliFails_ReturnsUnauthenticatedState()
    {
        using var scope = FakeMode("error");
        var provider = CreateProvider();

        var state = await provider.GetAccountStateAsync(CancellationToken.None);

        state.IsAuthenticated.Should().BeFalse();
        state.StatusText.Should().Contain("未登录");
    }

    [Fact]
    public async Task GetAccountState_WhenCliMissing_ReturnsUnauthenticatedStateWithoutThrowing()
    {
        var missingCli = Path.Combine(Path.GetTempPath(), "HanabePhotoManager.Tests", $"missing-{Guid.NewGuid():N}.cjs");
        var provider = new QuarkCloudProvider(cliPath: missingCli, nodePath: "node");

        var state = await provider.GetAccountStateAsync(CancellationToken.None);

        state.IsAuthenticated.Should().BeFalse();
        state.DisplayName.Should().Be("夸克网盘");
        state.StatusText.Should().Contain("未登录");
    }

    [Fact]
    public async Task List_MapsSearchPreviewAndArtifactEntries()
    {
        var artifactPath = CreateArtifact();
        using var scope = FakeMode("list", artifactPath);
        var provider = CreateProvider();

        var items = await CollectAsync(provider.ListAsync(new CloudPath("/"), CancellationToken.None));

        items.Should().HaveCount(4);
        items.Select(static item => item.RemoteId).Should().BeEquivalentTo(
            ["fid-folder", "fid-img", "fid-other", "fid-b"]);

        var folder = items.Single(static item => item.RemoteId == "fid-folder");
        folder.Kind.Should().Be(CloudObjectKind.Folder);
        folder.Name.Should().Be("相册");
        folder.Path.Value.Should().Be("/相册");

        var image = items.Single(static item => item.RemoteId == "fid-img");
        image.Kind.Should().Be(CloudObjectKind.Image);
        image.Name.Should().Be("a.jpg");
        image.Size.Should().Be(2048);
        image.Path.Value.Should().Be("/相册/a.jpg");

        var artifactImage = items.Single(static item => item.RemoteId == "fid-b");
        artifactImage.Name.Should().Be("b.jpg");
        artifactImage.Path.Value.Should().Be("/相册/b.jpg");
    }

    [Fact]
    public async Task List_Subdirectory_FiltersItemsByRealPath()
    {
        var artifactPath = CreateArtifact();
        using var scope = FakeMode("list", artifactPath);
        var provider = CreateProvider();

        var items = await CollectAsync(provider.ListAsync(new CloudPath("/相册"), CancellationToken.None));

        // /相册 目录下只有 a.jpg 与 b.jpg；x.jpg 在 /其他 下被过滤，相册文件夹本身是目录而非子项。
        items.Select(static item => item.RemoteId).Should().BeEquivalentTo(["fid-img", "fid-b"]);
    }

    [Fact]
    public async Task List_WhenNotLoggedIn_YieldsNoItemsWithoutThrowing()
    {
        using var scope = FakeMode("unauth");
        var provider = CreateProvider();

        var items = await CollectAsync(provider.ListAsync(new CloudPath("/"), CancellationToken.None));

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenThumbnail_ReturnsNull()
    {
        var provider = CreateProvider();
        var item = new CloudObject(
            CloudProviderKind.Quark, "fid-img", new CloudPath("/a.jpg"), "a.jpg",
            CloudObjectKind.Image, 10, DateTimeOffset.UtcNow, null, false);

        var thumbnail = await provider.OpenThumbnailAsync(item, CancellationToken.None);

        thumbnail.Should().BeNull();
    }

    [Fact]
    public async Task EnsureFolder_ParsesFidAndMapsFolderObject()
    {
        using var scope = FakeMode("authed");
        var provider = CreateProvider();

        var folder = await provider.EnsureFolderAsync(new CloudPath("/新建目录"), CancellationToken.None);

        folder.RemoteId.Should().Be("fid-newdir");
        folder.Kind.Should().Be(CloudObjectKind.Folder);
        folder.Name.Should().Be("新建目录");
        folder.Path.Value.Should().Be("/新建目录");
    }

    [Fact]
    public async Task Upload_ReturnsFidFromListRowAndReportsProgress()
    {
        var localFile = Path.Combine(Path.GetTempPath(), "HanabePhotoManager.Tests", $"upload-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(localFile, [1, 2, 3, 4]);
        try
        {
            using var scope = FakeMode("authed");
            var provider = CreateProvider();
            var progressReports = new List<CloudUploadProgress>();

            var remoteId = await provider.UploadAsync(
                localFile,
                new CloudPath("/"),
                new Progress<CloudUploadProgress>(progressReports.Add),
                CancellationToken.None);

            remoteId.Should().Be("fid-up");
            progressReports.Should().NotBeEmpty();
            progressReports.Should().OnlyContain(static report => report.TotalBytes == report.BytesTransferred);
        }
        finally
        {
            File.Delete(localFile);
        }
    }

    [Fact]
    public async Task Verify_WhenSearchFindsRemoteId_ReturnsVerified()
    {
        using var scope = FakeMode("verify-found");
        var provider = CreateProvider();
        var expected = new CloudTransferFile(
            Path.Combine(Path.GetTempPath(), "IMG_001.jpg"),
            new CloudRelativePath("相册/IMG_001.jpg"),
            2048,
            contentHash: null);

        var result = await provider.VerifyAsync("fid-verify", expected, CancellationToken.None);

        result.IsVerified.Should().BeTrue();
        result.RemoteId.Should().Be("fid-verify");
    }

    [Fact]
    public async Task Verify_WhenSearchDoesNotFindRemoteId_ReturnsUnverified()
    {
        using var scope = FakeMode("verify-missing");
        var provider = CreateProvider();
        var expected = new CloudTransferFile(
            Path.Combine(Path.GetTempPath(), "IMG_001.jpg"),
            new CloudRelativePath("相册/IMG_001.jpg"),
            2048,
            contentHash: null);

        var result = await provider.VerifyAsync("fid-verify", expected, CancellationToken.None);

        result.IsVerified.Should().BeFalse();
        result.RemoteId.Should().Be("fid-verify");
    }

    [Fact]
    public async Task Login_SucceedsWhenCliReportsSuccess()
    {
        using var scope = FakeMode("login-ok");
        var provider = CreateProvider();

        var succeeded = await provider.LoginAsync();

        succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Login_FailsWhenCliReportsError()
    {
        using var scope = FakeMode("unauth");
        var provider = CreateProvider();

        var succeeded = await provider.LoginAsync();

        succeeded.Should().BeFalse();
    }

    private static QuarkCloudProvider CreateProvider() =>
        new(cliPath: FakeCliPath.Value, nodePath: "node");

    private static async Task<IReadOnlyList<CloudObject>> CollectAsync(
        IAsyncEnumerable<CloudObject> source)
    {
        var items = new List<CloudObject>();
        await foreach (var item in source)
        {
            items.Add(item);
        }

        return items;
    }

    private static string CreateArtifact()
    {
        var path = Path.Combine(Path.GetTempPath(), "HanabePhotoManager.Tests", $"quark-artifact-{Guid.NewGuid():N}.jsonl");
        File.WriteAllText(
            path,
            """{"fid":"fid-b","filename":"b.jpg","category":3,"size":4096,"updated_at":1700000000000,"path":"/相册/b.jpg"}""" + "\n",
            new UTF8Encoding(false));
        return path;
    }

    private static FakeModeScope FakeMode(string mode, string? artifactPath = null) => new(mode, artifactPath);

    private static string CreateFakeCli()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HanabePhotoManager.Tests", $"quark-fake-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "fake-quark.cjs");
        File.WriteAllText(path, FakeCliScript, new UTF8Encoding(false));
        return path;
    }

    private const string FakeCliScript = """
        const mode = process.env.FAKE_QUARK_MODE || "unauth";
        const cmd = process.argv[2];
        function emit(obj) { process.stdout.write(JSON.stringify(obj) + "\n"); }

        if (cmd === "get-user-info") {
          if (mode === "authed") {
            emit({ code: 0, msg: "成功", data: { vipInfo: { vipType: 0, expiresIn: 86400, used: 1024, capacity: 10240 }, userInfo: { nickname: "测试用户" } }, action: "get-user-info", type: "result" });
          } else if (mode === "error") {
            emit({ code: -5, msg: "网络错误", data: {}, action: "get-user-info", type: "result" });
          } else {
            emit({ code: -103, msg: "未登录，请先执行 login 命令完成登录授权", action: "login", type: "result", data: {} });
          }
        } else if (cmd === "search") {
          if (mode === "verify-found") {
            emit({ code: 0, msg: "成功", data: { total: 1, file_list: [{ fid: "fid-verify", filename: "IMG_001.jpg", category: 3, size: 2048, updated_at: 1700000000000, path: "/相册/IMG_001.jpg" }] }, action: "search", type: "result" });
          } else if (mode === "verify-missing") {
            emit({ code: 0, msg: "成功", data: { total: 0, file_list: [] }, action: "search", type: "result" });
          } else if (mode === "list") {
            emit({ code: 0, msg: "成功", data: { total: 4, file_list: [
              { fid: "fid-folder", filename: "相册", category: 0, size: 0, updated_at: 1700000000000, path: "/相册" },
              { fid: "fid-img", filename: "a.jpg", category: 3, size: 2048, updated_at: 1700000000000, path: "/相册/a.jpg" },
              { fid: "fid-other", filename: "x.jpg", category: 3, size: 512, updated_at: 1700000000000, path: "/其他/x.jpg" }
            ] }, action: "search", type: "result" });
            emit({ code: 0, msg: "成功", data: { file_path: process.env.FAKE_QUARK_ARTIFACT || "", count: 1, format: "jsonl", description: "" }, action: "search", type: "artifact" });
          } else {
            emit({ code: 0, msg: "成功", data: { total: 0, file_list: [] }, action: "search", type: "result" });
          }
        } else if (cmd === "create-folder") {
          emit({ code: 0, msg: "成功", data: { fid: "fid-newdir", full_path: "/新建目录" }, action: "create-folder", type: "result" });
        } else if (cmd === "upload") {
          emit({ msg: "", action: "upload", type: "progress", data: { current: 100, total: 100, percent: 100 } });
          const pathArg = process.argv[3] || "p.jpg";
          const fileName = pathArg.split(/[\\/]/).pop();
          emit({ code: 0, msg: "", data: { recordId: "rec1", fileId: "fid-up", fileName: fileName, fileSize: 100, instantUpload: false }, action: "upload", type: "list" });
          emit({ code: 0, msg: "成功", data: { fileNames: [fileName], fileCount: 1, totalSize: 100, fids: ["fid-up"], successCount: 1 }, action: "upload", type: "result" });
        } else if (cmd === "login") {
          if (mode === "login-ok") {
            emit({ code: 0, msg: "授权成功", data: {}, action: "login", type: "result" });
          } else {
            emit({ code: -1001, msg: "授权失败", data: {}, action: "login", type: "result" });
          }
        }
        """;

    private sealed class FakeModeScope : IDisposable
    {
        private readonly string? _previousMode;
        private readonly string? _previousArtifact;

        public FakeModeScope(string mode, string? artifactPath)
        {
            _previousMode = Environment.GetEnvironmentVariable("FAKE_QUARK_MODE");
            _previousArtifact = Environment.GetEnvironmentVariable("FAKE_QUARK_ARTIFACT");
            Environment.SetEnvironmentVariable("FAKE_QUARK_MODE", mode);
            if (artifactPath is not null)
            {
                Environment.SetEnvironmentVariable("FAKE_QUARK_ARTIFACT", artifactPath);
            }
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("FAKE_QUARK_MODE", _previousMode);
            Environment.SetEnvironmentVariable("FAKE_QUARK_ARTIFACT", _previousArtifact);
        }
    }
}
