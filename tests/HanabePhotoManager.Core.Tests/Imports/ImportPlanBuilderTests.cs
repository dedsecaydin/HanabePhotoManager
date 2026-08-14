using FluentAssertions;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Core.Tests.Imports;

public sealed class ImportPlanBuilderTests
{
    [Theory]
    [InlineData(MediaCategory.Raw, "RAW生图")]
    [InlineData(MediaCategory.Jpeg, "JPG生图")]
    [InlineData(MediaCategory.Edited, "修后")]
    [InlineData(MediaCategory.Video, "视频")]
    [InlineData(MediaCategory.ActionVideo, "action视频")]
    [InlineData(MediaCategory.Material, "素材")]
    public async Task BuildAsync_UsesExpectedCategoryFolderNames(MediaCategory category, string folderName)
    {
        var source = CreateSource($@"D:\camera\{category}.dat");
        var group = new MediaGroup(category.ToString(), category, source, Array.Empty<SourceMediaFile>());
        var probe = new RecordingProbe(_ => ConflictKind.None);

        var plan = await new ImportPlanBuilder(probe)
            .BuildAsync(@"E:\library", new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [group], CancellationToken.None);

        var file = plan.Items.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
        file.DestinationPath.Should().Be(Path.Combine(@"E:\library", "7月", "07.11", folderName, "JK0001" + Path.GetExtension(source.FullPath).ToUpperInvariant()));
        file.TemporaryPath.Should().Be(file.DestinationPath + ".hanabe-part");
    }

    [Fact]
    public async Task BuildAsync_PlansPrimaryThenSidecarsAndPreservesInstances()
    {
        var primary = CreateSource(@"D:\camera\C0001.MP4");
        var sidecar1 = CreateSource(@"D:\camera\C0001M01.XML");
        var sidecar2 = CreateSource(@"D:\camera\C0001M02.XML");
        var group = new MediaGroup("C0001", MediaCategory.Video, primary, [sidecar1, sidecar2]);

        var plan = await new ImportPlanBuilder(new RecordingProbe(_ => ConflictKind.None))
            .BuildAsync(@"E:\library", new LibraryDate(2026, 7, 11), TransferMode.MoveAfterVerify, [group], CancellationToken.None);

        var item = plan.Items.Should().ContainSingle().Subject;
        item.Group.Should().BeSameAs(group);
        item.State.Should().Be(ImportItemState.Planned);
        item.Files.Select(file => file.Source).Should().Equal(primary, sidecar1, sidecar2);
        item.Files[0].Source.Should().BeSameAs(primary);
        item.Files[1].Source.Should().BeSameAs(sidecar1);
        item.Files[2].Source.Should().BeSameAs(sidecar2);
        item.Files.Select(file => Path.GetFileName(file.DestinationPath)).Should().Equal("JK0001.MP4", "JK0001.XML", "JK0001_02.XML");
    }

    [Fact]
    public async Task BuildAsync_UsesNaturalOrderWhenAssigningSequenceNames()
    {
        var dsc10 = new MediaGroup("DSC10", MediaCategory.Jpeg, CreateSource(@"D:\camera\DSC10.JPG"), Array.Empty<SourceMediaFile>());
        var dsc1988 = new MediaGroup("DSC1988", MediaCategory.Jpeg, CreateSource(@"D:\camera\DSC1988.JPG"), Array.Empty<SourceMediaFile>());
        var dsc9 = new MediaGroup("DSC9", MediaCategory.Jpeg, CreateSource(@"D:\camera\DSC9.JPG"), Array.Empty<SourceMediaFile>());
        var dsc1987 = new MediaGroup("DSC1987", MediaCategory.Jpeg, CreateSource(@"D:\camera\DSC1987.JPG"), Array.Empty<SourceMediaFile>());

        var plan = await new ImportPlanBuilder(new RecordingProbe(_ => ConflictKind.None))
            .BuildAsync(@"E:\library", new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [dsc10, dsc1988, dsc9, dsc1987], CancellationToken.None);

        FileNameFor(plan, dsc9).Should().Be("JK0001.JPG");
        FileNameFor(plan, dsc10).Should().Be("JK0002.JPG");
        FileNameFor(plan, dsc1987).Should().Be("JK0003.JPG");
        FileNameFor(plan, dsc1988).Should().Be("JK0004.JPG");
    }

    [Fact]
    public async Task BuildAsync_GivesSameSequenceToVideoAndAudioBackupWithSameName()
    {
        var video = CreateSource(@"D:\camera\C0001.MP4");
        var audioBackup = CreateSource(@"D:\camera\C0001.AAC");
        var group = new MediaGroup("C0001", MediaCategory.Video, video, [audioBackup]);

        var plan = await new ImportPlanBuilder(new RecordingProbe(_ => ConflictKind.None))
            .BuildAsync(@"E:\library", new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [group], CancellationToken.None);

        plan.Items.Single().Files.Select(file => Path.GetFileName(file.DestinationPath))
            .Should().Equal("JK0001.MP4", "JK0001.AAC");
    }

    [Fact]
    public async Task BuildAsync_ContinuesAfterExistingSequenceFilesInTargetCategory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "HanabePhotoManagerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var categoryRoot = Path.Combine(tempRoot, "7月", "07.11", "JPG生图");
            Directory.CreateDirectory(categoryRoot);
            File.WriteAllText(Path.Combine(categoryRoot, "JK0007.JPG"), "existing");

            var group = new MediaGroup("photo", MediaCategory.Jpeg, CreateSource(@"D:\camera\photo.JPG"), Array.Empty<SourceMediaFile>());
            var plan = await new ImportPlanBuilder(new RecordingProbe(_ => ConflictKind.None))
                .BuildAsync(tempRoot, new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [group], CancellationToken.None);

            Path.GetFileName(plan.Items.Single().Files.Single().DestinationPath).Should().Be("JK0008.JPG");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(ConflictKind.SameNameDifferentContent, ConflictKind.None, ConflictKind.SameNameDifferentContent)]
    [InlineData(ConflictKind.Identical, ConflictKind.Identical, ConflictKind.Identical)]
    [InlineData(ConflictKind.Identical, ConflictKind.None, ConflictKind.None)]
    [InlineData(ConflictKind.None, ConflictKind.None, ConflictKind.None)]
    public async Task BuildAsync_AggregatesFileConflicts(ConflictKind first, ConflictKind second, ConflictKind expected)
    {
        var group = new MediaGroup("C0001", MediaCategory.Video, CreateSource(@"D:\camera\C0001.MP4"), [CreateSource(@"D:\camera\C0001M01.XML")]);
        var conflicts = new Queue<ConflictKind>([first, second]);

        var plan = await new ImportPlanBuilder(new RecordingProbe(_ => conflicts.Dequeue()))
            .BuildAsync(@"E:\library", new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [group], CancellationToken.None);

        plan.Items.Single().Files.Select(file => file.Conflict).Should().Equal(first, second);
        plan.Items.Single().Conflict.Should().Be(expected);
    }

    [Fact]
    public async Task BuildAsync_PreservesInputGroupOrderDeterministically()
    {
        var first = new MediaGroup("b", MediaCategory.Jpeg, CreateSource(@"D:\camera\b.JPG"), Array.Empty<SourceMediaFile>());
        var second = new MediaGroup("a", MediaCategory.Raw, CreateSource(@"D:\camera\a.ARW"), Array.Empty<SourceMediaFile>());

        var plan = await new ImportPlanBuilder(new RecordingProbe(_ => ConflictKind.None))
            .BuildAsync(@"E:\library", new LibraryDate(2026, 7, 11), TransferMode.CopyThenAskDelete, [first, second], CancellationToken.None);

        plan.Items.Select(item => item.Group).Should().Equal(first, second);
        plan.Items.SelectMany(item => item.Files).Select(file => file.Source).Should().Equal(first.Primary, second.Primary);
    }

    [Fact]
    public async Task BuildAsync_FlowsCancellationTokenToProbe()
    {
        using var cts = new CancellationTokenSource();
        var expectedToken = cts.Token;
        var probe = new RecordingProbe(_ => ConflictKind.None);
        var group = new MediaGroup("photo", MediaCategory.Jpeg, CreateSource(@"D:\camera\photo.JPG"), Array.Empty<SourceMediaFile>());

        await new ImportPlanBuilder(probe)
            .BuildAsync(@"E:\library", new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [group], expectedToken);

        probe.Calls.Should().ContainSingle().Which.CancellationToken.Should().Be(expectedToken);
    }

    [Theory]
    [InlineData(MediaCategory.Unconfirmed)]
    [InlineData((MediaCategory)999)]
    public async Task BuildAsync_RejectsUnconfirmedOrUnknownCategories(MediaCategory category)
    {
        var group = new MediaGroup("manual", category, CreateSource(@"D:\camera\manual.bin"), Array.Empty<SourceMediaFile>());

        var act = () => new ImportPlanBuilder(new RecordingProbe(_ => ConflictKind.None))
            .BuildAsync(@"E:\library", new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [group], CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*category*");
    }

    [Fact]
    public async Task BuildAsync_RejectsInvalidArguments()
    {
        var builder = new ImportPlanBuilder(new RecordingProbe(_ => ConflictKind.None));
        var group = new MediaGroup("photo", MediaCategory.Jpeg, CreateSource(@"D:\camera\photo.JPG"), Array.Empty<SourceMediaFile>());

        await builder.Invoking(b => b.BuildAsync(null!, new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [group], CancellationToken.None))
            .Should().ThrowAsync<ArgumentNullException>();
        await builder.Invoking(b => b.BuildAsync("   ", new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [group], CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
        await builder.Invoking(b => b.BuildAsync(@"E:\library", new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, null!, CancellationToken.None))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BuildAsync_PassesSourceAndDestinationToProbe()
    {
        var source = CreateSource(@"D:\camera\photo.JPG");
        var group = new MediaGroup("photo", MediaCategory.Jpeg, source, Array.Empty<SourceMediaFile>());
        var probe = new RecordingProbe(_ => ConflictKind.Identical);

        await new ImportPlanBuilder(probe)
            .BuildAsync(@"E:\library", new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [group], CancellationToken.None);

        var call = probe.Calls.Should().ContainSingle().Subject;
        call.Source.Should().BeSameAs(source);
        call.Destination.Should().Be(Path.Combine(@"E:\library", "7月", "07.11", "JPG生图", "JK0001.JPG"));
    }

    [Fact]
    public async Task BuildAsync_NormalizesRootRelativeLibraryRootToFullyQualifiedDestinations()
    {
        // 回归：settings.json 里 LibraryRoot 曾为 "\Hanabe\拍照"（根相对路径、无盘符），
        // 旧代码直接 Path.Combine 产出 "\Hanabe\拍照\8月\08.15\..." 无盘符目标，
        // 下游 VerifiedFileTransfer 抛 "Transfer paths must be fully qualified." 中断整批导入。
        // GetFullPath 会把根相对路径解析成当前盘符的绝对路径（本机为 C:\Hanabe\拍照）。
        var root = @"\HanabePhotoTests\拍照";
        var group = new MediaGroup("photo", MediaCategory.Jpeg, CreateSource(@"D:\camera\photo.JPG"), Array.Empty<SourceMediaFile>());
        var probe = new RecordingProbe(_ => ConflictKind.None);

        var plan = await new ImportPlanBuilder(probe)
            .BuildAsync(root, new LibraryDate(2026, 8, 15), TransferMode.CopyKeepSource, [group], CancellationToken.None);

        var expectedRoot = Path.GetFullPath(root);
        Path.IsPathFullyQualified(expectedRoot).Should().BeTrue();
        plan.LibraryRoot.Should().Be(expectedRoot);

        var file = plan.Items.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
        Path.IsPathFullyQualified(file.DestinationPath).Should().BeTrue();
        Path.IsPathFullyQualified(file.TemporaryPath).Should().BeTrue();
        file.DestinationPath.Should().Be(Path.Combine(expectedRoot, "8月", "08.15", "JPG生图", "JK0001.JPG"));
    }

    [Fact]
    public async Task BuildAsync_KeepsFullyQualifiedLibraryRootUnchanged()
    {
        // 已完全限定的根（含盘符/UNC）保持原样，GetFullPath 不改变其语义。
        var root = @"E:\library";
        var group = new MediaGroup("photo", MediaCategory.Jpeg, CreateSource(@"D:\camera\photo.JPG"), Array.Empty<SourceMediaFile>());

        var plan = await new ImportPlanBuilder(new RecordingProbe(_ => ConflictKind.None))
            .BuildAsync(root, new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [group], CancellationToken.None);

        plan.LibraryRoot.Should().Be(@"E:\library");
        var file = plan.Items.Single().Files.Single();
        file.DestinationPath.Should().Be(Path.Combine(@"E:\library", "7月", "07.11", "JPG生图", "JK0001.JPG"));
    }

    [Fact]
    public async Task BuildAsync_KeepsUncLibraryRootAndProducesUncDestinations()
    {
        // 回归：真实照片库是 UNC 共享 "\\Hanabe\拍照"（另一台电脑 HANABE），
        // 根与 Destination 必须保持 UNC 前缀，绝不变成 "C:\..." 盘路径。
        var root = @"\\Hanabe\拍照";
        var group = new MediaGroup("photo", MediaCategory.Jpeg, CreateSource(@"D:\camera\photo.JPG"), Array.Empty<SourceMediaFile>());

        var plan = await new ImportPlanBuilder(new RecordingProbe(_ => ConflictKind.None))
            .BuildAsync(root, new LibraryDate(2026, 8, 15), TransferMode.CopyKeepSource, [group], CancellationToken.None);

        plan.LibraryRoot.Should().Be(root);
        var file = plan.Items.Single().Files.Single();
        file.DestinationPath.Should().Be(Path.Combine(root, "8月", "08.15", "JPG生图", "JK0001.JPG"));
        file.DestinationPath.Should().StartWith(@"\\Hanabe\拍照");
        file.DestinationPath.Should().NotStartWith(@"C:\");
        Path.IsPathFullyQualified(file.DestinationPath).Should().BeTrue();
        Path.IsPathFullyQualified(file.TemporaryPath).Should().BeTrue();
    }

    [Fact]
    public async Task BuildAsync_SingleBackslashRootRelative_PrefersReachableUnc()
    {
        // 根相对路径 "\Hanabe\拍照"（丢失反斜杠的 UNC）：真实共享可访问时应产出 UNC
        // Destination，而不是 GetFullPath 成的 C 盘路径（C:\Hanabe\拍照 只是本机残留副本）。
        if (!Directory.Exists(@"\\Hanabe\拍照"))
        {
            return; // 环境无该 UNC 共享时跳过端到端验证（分支逻辑由 LibraryRootNormalizerTests 确定性覆盖）
        }

        var group = new MediaGroup("photo", MediaCategory.Jpeg, CreateSource(@"D:\camera\photo.JPG"), Array.Empty<SourceMediaFile>());

        var plan = await new ImportPlanBuilder(new RecordingProbe(_ => ConflictKind.None))
            .BuildAsync(@"\Hanabe\拍照", new LibraryDate(2026, 8, 15), TransferMode.CopyKeepSource, [group], CancellationToken.None);

        plan.LibraryRoot.Should().Be(@"\\Hanabe\拍照");
        plan.LibraryRoot.Should().NotBe(Path.GetFullPath(@"\Hanabe\拍照"));
        var file = plan.Items.Single().Files.Single();
        file.DestinationPath.Should().StartWith(@"\\Hanabe\拍照\8月\08.15\JPG生图\");
        file.DestinationPath.Should().NotStartWith(@"C:\");
        Path.IsPathFullyQualified(file.DestinationPath).Should().BeTrue();
    }

    private static string FileNameFor(ImportPlan plan, MediaGroup group)
    {
        return Path.GetFileName(plan.Items.Single(item => ReferenceEquals(item.Group, group)).Files.Single().DestinationPath);
    }

    private static SourceMediaFile CreateSource(string fullPath)
    {
        return new SourceMediaFile(fullPath, 42, DateTimeOffset.UnixEpoch);
    }

    private sealed class RecordingProbe(Func<SourceMediaFile, ConflictKind> resolve) : IDestinationProbe
    {
        private readonly Func<SourceMediaFile, ConflictKind> _resolve = resolve;

        public List<ProbeCall> Calls { get; } = [];

        public Task<ConflictKind> CheckAsync(SourceMediaFile source, string destination, CancellationToken cancellationToken)
        {
            Calls.Add(new ProbeCall(source, destination, cancellationToken));
            return Task.FromResult(_resolve(source));
        }
    }

    private sealed record ProbeCall(SourceMediaFile Source, string Destination, CancellationToken CancellationToken);
}

public sealed class ImportPlanBuilderDuplicateDestinationTests
{
    [Fact]
    public async Task BuildAsync_UsesDifferentSequenceNamesForSameOriginalFileName()
    {
        var first = new MediaGroup("first", MediaCategory.Jpeg, CreateSource(@"D:\camera-a\photo.JPG"), Array.Empty<SourceMediaFile>());
        var second = new MediaGroup("second", MediaCategory.Jpeg, CreateSource(@"D:\camera-b\PHOTO.JPG"), Array.Empty<SourceMediaFile>());
        var probe = new AlwaysNoneProbe();

        var plan = await new ImportPlanBuilder(probe)
            .BuildAsync(@"E:\library", new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [first, second], CancellationToken.None);

        plan.Items.SelectMany(item => item.Files).Select(file => Path.GetFileName(file.DestinationPath))
            .Should().Equal("JK0001.JPG", "JK0002.JPG");
        plan.Items.Select(item => item.Conflict).Should().OnlyContain(conflict => conflict == ConflictKind.None);
        probe.Calls.Should().Be(2);
    }

    private static SourceMediaFile CreateSource(string fullPath)
    {
        return new SourceMediaFile(fullPath, 42, DateTimeOffset.UnixEpoch);
    }

    private sealed class AlwaysNoneProbe : IDestinationProbe
    {
        public int Calls { get; private set; }

        public Task<ConflictKind> CheckAsync(SourceMediaFile source, string destination, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(ConflictKind.None);
        }
    }
}
