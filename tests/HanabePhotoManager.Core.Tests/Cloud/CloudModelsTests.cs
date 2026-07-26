using FluentAssertions;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.Core.Tests.Cloud;

public sealed class CloudModelsTests
{
    [Fact]
    public void CloudPath_IsAnImmutableReferenceValueObject()
    {
        typeof(CloudPath).IsValueType.Should().BeFalse();
        typeof(CloudPath).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void CloudPath_NormalizedValuesAreEqual()
    {
        var first = new CloudPath(@"\Hanabe照片备份\\7月\07.14\");
        var second = new CloudPath("/Hanabe照片备份/7月/07.14");

        first.Should().Be(second);
    }

    [Fact]
    public void CloudPath_NormalizesSeparatorsAndEmptySegments()
    {
        var path = new CloudPath("/Hanabe照片备份//7月/07.14/");

        path.Value.Should().Be("/Hanabe照片备份/7月/07.14");
    }

    [Fact]
    public void CloudPath_ConvertsBackslashes()
    {
        var path = new CloudPath(@"\Hanabe照片备份\7月\07.14");

        path.Value.Should().Be("/Hanabe照片备份/7月/07.14");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("////")]
    public void CloudPath_NormalizesRoot(string input)
    {
        new CloudPath(input).Value.Should().Be("/");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void CloudPath_RejectsBlankValue(string? input)
    {
        var act = () => new CloudPath(input!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("/photos/./photo.jpg")]
    [InlineData(@"\photos\..\photo.jpg")]
    public void CloudPath_RejectsTraversalSegments(string input)
    {
        var act = () => new CloudPath(input);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("C:/photos/photo.jpg")]
    [InlineData(@"C:\photos\photo.jpg")]
    [InlineData("C:photos/photo.jpg")]
    [InlineData(@"\\server\share\photo.jpg")]
    [InlineData(@"\\?\C:\photos\photo.jpg")]
    [InlineData(@"\\.\C:\photos\photo.jpg")]
    public void CloudPath_RejectsWindowsLocalPathSyntax(string input)
    {
        var act = () => new CloudPath(input);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(@"\??\C:\photos\photo.jpg")]
    [InlineData(@"/??/C:/photos/photo.jpg")]
    [InlineData(@"\Device\HarddiskVolume1\photo.jpg")]
    [InlineData(@"/dEvIcE/HarddiskVolume1/photo.jpg")]
    [InlineData(@"\GLOBALROOT\Device\photo.jpg")]
    [InlineData(@"/globalroot/Device/photo.jpg")]
    [InlineData("//server/share/photo.jpg")]
    [InlineData(@"\\SERVER\SHARE\photo.jpg")]
    [InlineData(@"\/server/share/photo.jpg")]
    public void CloudPath_RejectsAuthorityOrNtNamespaceSyntax(string input)
    {
        var act = () => new CloudPath(input);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("/photos/2026/photo.jpg")]
    [InlineData("/photos/Device/photo.jpg")]
    [InlineData("/photos/GLOBALROOT/photo.jpg")]
    public void CloudPath_AcceptsNormalPosixAbsolutePath(string input)
    {
        new CloudPath(input).Value.Should().Be(input);
    }

    [Fact]
    public void CloudRelativePath_NormalizesWithoutLeadingSlash()
    {
        var path = new CloudRelativePath(@"7月\\07.14//source.jpg/");

        path.Value.Should().Be("7月/07.14/source.jpg");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    [InlineData(@"////\")]
    public void CloudRelativePath_RejectsBlankOrRoot(string? input)
    {
        var act = () => new CloudRelativePath(input!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("photos/./photo.jpg")]
    [InlineData(@"photos\..\photo.jpg")]
    public void CloudRelativePath_RejectsTraversalSegments(string input)
    {
        var act = () => new CloudRelativePath(input);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("/photos/photo.jpg")]
    [InlineData(@"\photos\photo.jpg")]
    [InlineData("C:/photos/photo.jpg")]
    [InlineData(@"C:\photos\photo.jpg")]
    [InlineData("C:photos/photo.jpg")]
    [InlineData(@"\\server\share\photo.jpg")]
    [InlineData(@"\\?\C:\photos\photo.jpg")]
    [InlineData(@"\\.\C:\photos\photo.jpg")]
    public void CloudRelativePath_RejectsRootedOrDriveQualifiedInput(string input)
    {
        var act = () => new CloudRelativePath(input);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(@"??\C:\photos\photo.jpg")]
    [InlineData("??/C:/photos/photo.jpg")]
    [InlineData(@"Device\HarddiskVolume1\photo.jpg")]
    [InlineData("dEvIcE/HarddiskVolume1/photo.jpg")]
    [InlineData(@"GLOBALROOT\Device\photo.jpg")]
    [InlineData("globalroot/Device/photo.jpg")]
    [InlineData("//server/share/photo.jpg")]
    [InlineData(@"\\SERVER\SHARE\photo.jpg")]
    [InlineData(@"\/server/share/photo.jpg")]
    public void CloudRelativePath_RejectsAuthorityOrNtNamespaceSyntax(string input)
    {
        var act = () => new CloudRelativePath(input);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudRelativePath_NormalizedValuesAreEqual()
    {
        var first = new CloudRelativePath(@"7月\\07.14\source.jpg");
        var second = new CloudRelativePath("7月/07.14/source.jpg/");

        first.Should().Be(second);
    }

    [Theory]
    [InlineData("/", "7月/07.14/source.jpg", "/7月/07.14/source.jpg")]
    [InlineData("/backup/", @"7月\\07.14\source.jpg", "/backup/7月/07.14/source.jpg")]
    public void CloudPath_CombinesRelativePath(
        string destination,
        string relativePath,
        string expected)
    {
        var combined = new CloudPath(destination).Combine(new CloudRelativePath(relativePath));

        combined.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(typeof(CloudProviderKind))]
    [InlineData(typeof(CloudObjectKind))]
    [InlineData(typeof(CloudTransferPriority))]
    [InlineData(typeof(CloudTransferState))]
    public void PersistedEnums_DoNotDefineZero(Type enumType)
    {
        Enum.IsDefined(enumType, 0).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void CloudAccountState_RejectsUndefinedProvider(int provider)
    {
        var act = () => new CloudAccountState(
            (CloudProviderKind)provider,
            true,
            "account",
            0,
            100,
            "connected");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, -1)]
    [InlineData(101, 100)]
    public void CloudAccountState_RejectsInvalidCapacity(long usedBytes, long totalBytes)
    {
        var act = () => new CloudAccountState(
            CloudProviderKind.Quark,
            true,
            "account",
            usedBytes,
            totalBytes,
            "connected");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudAccountState_RejectsBlankDisplayName(string? displayName)
    {
        var act = () => new CloudAccountState(
            CloudProviderKind.Quark,
            true,
            displayName!,
            0,
            100,
            "connected");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudAccountState_RejectsBlankStatusText(string? statusText)
    {
        var act = () => new CloudAccountState(
            CloudProviderKind.Quark,
            true,
            "account",
            0,
            100,
            statusText!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void CloudObject_RejectsUndefinedProvider(int provider)
    {
        var act = () => CreateObject(provider: (CloudProviderKind)provider);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void CloudObject_RejectsUndefinedKind(int kind)
    {
        var act = () => CreateObject(kind: (CloudObjectKind)kind);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CloudObject_RejectsNullPath()
    {
        var act = () => new CloudObject(
            CloudProviderKind.Quark,
            "remote-id",
            null!,
            "photo.jpg",
            CloudObjectKind.Image,
            42,
            DateTimeOffset.UnixEpoch,
            null,
            true);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudObject_RejectsBlankRemoteId(string? remoteId)
    {
        var act = () => CreateObject(remoteId: remoteId!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudObject_RejectsBlankName(string? name)
    {
        var act = () => CreateObject(name: name!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudObject_RejectsNegativeSize()
    {
        var act = () => CreateObject(size: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, -1)]
    [InlineData(101, 100)]
    public void CloudUploadProgress_RejectsInvalidByteCounts(
        long bytesTransferred,
        long totalBytes)
    {
        var act = () => new CloudUploadProgress(bytesTransferred, totalBytes, "photo.jpg");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudUploadProgress_RejectsBlankCurrentFile(string? currentFile)
    {
        var act = () => new CloudUploadProgress(0, 100, currentFile!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudTransferFile_RejectsBlankLocalPath(string? localPath)
    {
        var act = () => new CloudTransferFile(
            localPath!,
            new CloudRelativePath("photo.jpg"),
            42,
            null);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData(@"photos\photo.jpg")]
    [InlineData(@"\photos\photo.jpg")]
    [InlineData(@"C:photo.jpg")]
    public void CloudTransferFile_RejectsNonAbsoluteLocalPath(string localPath)
    {
        var act = () => new CloudTransferFile(
            localPath,
            new CloudRelativePath("photo.jpg"),
            42,
            null);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(@"C:\photos\photo.jpg")]
    [InlineData("C:/photos/photo.jpg")]
    [InlineData(@"\\server\share\photo.jpg")]
    public void CloudTransferFile_AcceptsAbsoluteWindowsLocalPath(string localPath)
    {
        var file = new CloudTransferFile(
            localPath,
            new CloudRelativePath("photo.jpg"),
            42,
            null);

        file.LocalPath.Should().Be(localPath);
    }

    [Fact]
    public void CloudTransferFile_AcceptsAbsolutePosixLocalPath()
    {
        const string localPath = "/Users/test/photos/photo.jpg";

        var file = new CloudTransferFile(
            localPath,
            new CloudRelativePath("photo.jpg"),
            42,
            null);

        file.LocalPath.Should().Be(localPath);
    }

    [Fact]
    public void CloudTransferFile_RejectsNullRelativePath()
    {
        var act = () => new CloudTransferFile("C:/photo.jpg", null!, 42, null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CloudTransferFile_RejectsNegativeSize()
    {
        var act = () => new CloudTransferFile(
            "C:/photo.jpg",
            new CloudRelativePath("photo.jpg"),
            -1,
            null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(43)]
    public void CloudTransferFile_RejectsUploadedBytesOutsideFileSize(long uploadedBytes)
    {
        var act = () => new CloudTransferFile(
            "C:/photo.jpg",
            new CloudRelativePath("photo.jpg"),
            42,
            null,
            uploadedBytes);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudTransferFile_RejectsBlankRemoteIdWhenProvided(string remoteId)
    {
        var act = () => new CloudTransferFile(
            "C:/photo.jpg",
            new CloudRelativePath("photo.jpg"),
            42,
            null,
            remoteId: remoteId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferFile_WithProgressReturnsValidatedCopy()
    {
        var original = CreateFile();

        var updated = original.WithProgress(21);

        updated.Should().NotBeSameAs(original);
        updated.UploadedBytes.Should().Be(21);
        original.UploadedBytes.Should().Be(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(43)]
    public void CloudTransferFile_WithProgressRejectsInvalidValue(long uploadedBytes)
    {
        var act = () => CreateFile().WithProgress(uploadedBytes);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CloudTransferFile_WithRemoteIdReturnsValidatedCopy()
    {
        var original = CreateFile();

        var updated = original.WithRemoteId("remote-id");

        updated.Should().NotBeSameAs(original);
        updated.RemoteId.Should().Be("remote-id");
        original.RemoteId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudTransferFile_WithRemoteIdRejectsBlankValue(string? remoteId)
    {
        var act = () => CreateFile().WithRemoteId(remoteId!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_UsesEntityIdentitySemantics()
    {
        typeof(IEquatable<CloudTransferJob>).IsAssignableFrom(typeof(CloudTransferJob))
            .Should().BeFalse();
    }

    [Fact]
    public void CloudTransferJob_RejectsEmptyId()
    {
        var act = () => CreateJob(id: Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsNullDestination()
    {
        var act = () => new CloudTransferJob(
            Guid.NewGuid(),
            CloudProviderKind.Quark,
            null!,
            CloudTransferPriority.Required,
            CloudTransferState.Pending,
            [CreateFile()],
            DateTimeOffset.UnixEpoch);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void CloudTransferJob_RejectsUndefinedProvider(int provider)
    {
        var act = () => CreateJob(provider: (CloudProviderKind)provider);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void CloudTransferJob_RejectsUndefinedPriority(int priority)
    {
        var act = () => CreateJob(priority: (CloudTransferPriority)priority);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void CloudTransferJob_RejectsUndefinedState(int state)
    {
        var act = () => CreateJob(state: (CloudTransferState)state);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CloudTransferJob_StateHasNoPublicSetter()
    {
        typeof(CloudTransferJob).GetProperty(nameof(CloudTransferJob.State))!
            .SetMethod.Should().BeNull();
    }

    [Fact]
    public void CloudTransferJob_WithStateReturnsValidatedCopy()
    {
        var original = CreateJob();

        var updated = original.WithState(CloudTransferState.Paused);

        updated.Should().NotBeSameAs(original);
        updated.State.Should().Be(CloudTransferState.Paused);
        original.State.Should().Be(CloudTransferState.Pending);
        updated.Id.Should().Be(original.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void CloudTransferJob_WithStateRejectsUndefinedValue(int state)
    {
        var act = () => CreateJob().WithState((CloudTransferState)state);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CloudTransferJob_WithStateRejectsCompleted()
    {
        var act = () => CreateJob().WithState(CloudTransferState.Completed);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsCompletedWithIncompleteFiles()
    {
        var act = () => CreateJob(state: CloudTransferState.Completed);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsCompletedWithoutVerificationEvidence()
    {
        var act = () => CreateJob(
            state: CloudTransferState.Completed,
            files: [CreateReadyFile()]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_CannotCompleteWithTimestampAlone()
    {
        typeof(CloudTransferJob).GetMethod(
                nameof(CloudTransferJob.MarkVerified),
                [typeof(DateTimeOffset)])
            .Should().BeNull();
    }

    [Fact]
    public void CloudTransferJob_MarkVerifiedPersistsEvidenceForEveryFile()
    {
        var files = new[]
        {
            CreateReadyFile("remote-a", "a.jpg"),
            CreateReadyFile("remote-b", "b.jpg")
        };
        var original = CreateJob(files: files);
        var verifiedAt = DateTimeOffset.UnixEpoch.AddHours(1);
        var results = new[]
        {
            new CloudVerificationResult(true, "b verified", "remote-b"),
            new CloudVerificationResult(true, "a verified", "remote-a")
        };

        var completed = original.MarkVerified(results, verifiedAt);

        completed.State.Should().Be(CloudTransferState.Completed);
        completed.IsVerified.Should().BeTrue();
        completed.FileVerifications.Select(item => item.RemoteId)
            .Should().Equal("remote-a", "remote-b");
        completed.FileVerifications.Select(item => item.VerifiedAt)
            .Should().OnlyContain(item => item == verifiedAt);
        completed.FileVerifications.Select(item => item.Reason)
            .Should().Equal("a verified", "b verified");
        original.State.Should().Be(CloudTransferState.Pending);
    }

    [Fact]
    public void CloudTransferJob_MarkVerifiedRejectsIncompleteFiles()
    {
        var results = new[] { new CloudVerificationResult(true, "verified", "remote-id") };

        var act = () => CreateJob().MarkVerified(results, DateTimeOffset.UnixEpoch);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CloudTransferJob_MarkVerifiedRejectsPartialResults()
    {
        var job = CreateJob(files:
        [
            CreateReadyFile("remote-a", "a.jpg"),
            CreateReadyFile("remote-b", "b.jpg")
        ]);
        var results = new[] { new CloudVerificationResult(true, "verified", "remote-a") };

        var act = () => job.MarkVerified(results, DateTimeOffset.UnixEpoch.AddHours(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CloudTransferJob_MarkVerifiedRejectsFailedResult()
    {
        var job = CreateJob(files: [CreateReadyFile("remote-a", "a.jpg")]);
        var results = new[] { new CloudVerificationResult(false, "hash mismatch", "remote-a") };

        var act = () => job.MarkVerified(results, DateTimeOffset.UnixEpoch.AddHours(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CloudTransferJob_MarkVerifiedRejectsDuplicateResults()
    {
        var job = CreateJob(files:
        [
            CreateReadyFile("remote-a", "a.jpg"),
            CreateReadyFile("remote-b", "b.jpg")
        ]);
        var results = new[]
        {
            new CloudVerificationResult(true, "first", "remote-a"),
            new CloudVerificationResult(true, "duplicate", "remote-a")
        };

        var act = () => job.MarkVerified(results, DateTimeOffset.UnixEpoch.AddHours(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CloudTransferJob_MarkVerifiedRejectsMismatchedRemoteId()
    {
        var job = CreateJob(files: [CreateReadyFile("remote-a", "a.jpg")]);
        var results = new[] { new CloudVerificationResult(true, "verified", "REMOTE-A") };

        var act = () => job.MarkVerified(results, DateTimeOffset.UnixEpoch.AddHours(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CloudTransferJob_RestoresCompletedJobWithConsistentFileEvidence()
    {
        var files = new[]
        {
            CreateReadyFile("remote-a", "a.jpg"),
            CreateReadyFile("remote-b", "b.jpg")
        };
        var evidence = new[]
        {
            CreateEvidence("remote-a", "a verified"),
            CreateEvidence("remote-b", "b verified")
        };

        var restored = new CloudTransferJob(
            Guid.NewGuid(),
            CloudProviderKind.Quark,
            new CloudPath("/backup"),
            CloudTransferPriority.Required,
            CloudTransferState.Completed,
            files,
            DateTimeOffset.UnixEpoch,
            evidence);

        restored.IsVerified.Should().BeTrue();
        restored.FileVerifications.Should().HaveCount(2);
    }

    [Fact]
    public void CloudTransferJob_RejectsCompletedEvidenceWithIncompleteFiles()
    {
        var act = () => new CloudTransferJob(
            Guid.NewGuid(),
            CloudProviderKind.Quark,
            new CloudPath("/backup"),
            CloudTransferPriority.Required,
            CloudTransferState.Completed,
            [CreateFile()],
            DateTimeOffset.UnixEpoch,
            [CreateEvidence("remote-id")]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsVerificationEvidenceForNonCompletedState()
    {
        var act = () => new CloudTransferJob(
            Guid.NewGuid(),
            CloudProviderKind.Quark,
            new CloudPath("/backup"),
            CloudTransferPriority.Required,
            CloudTransferState.Running,
            [CreateReadyFile()],
            DateTimeOffset.UnixEpoch,
            [CreateEvidence("remote-id")]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsVerificationEvidenceBeforeCreation()
    {
        var act = () => new CloudTransferJob(
            Guid.NewGuid(),
            CloudProviderKind.Quark,
            new CloudPath("/backup"),
            CloudTransferPriority.Required,
            CloudTransferState.Completed,
            [CreateReadyFile()],
            DateTimeOffset.UnixEpoch.AddHours(2),
            [CreateEvidence("remote-id", verifiedAt: DateTimeOffset.UnixEpoch.AddHours(1))]);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsPartialPersistedVerificationEvidence()
    {
        var files = new[]
        {
            CreateReadyFile("remote-a", "a.jpg"),
            CreateReadyFile("remote-b", "b.jpg")
        };

        var act = () => RestoreCompleted(files, [CreateEvidence("remote-a")]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsFailedPersistedVerificationEvidence()
    {
        var evidence = new CloudFileVerification(
            "remote-a",
            DateTimeOffset.UnixEpoch.AddHours(1),
            false,
            "hash mismatch");

        var act = () => RestoreCompleted(
            [CreateReadyFile("remote-a", "a.jpg")],
            [evidence]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsDuplicatePersistedVerificationEvidence()
    {
        var files = new[]
        {
            CreateReadyFile("remote-a", "a.jpg"),
            CreateReadyFile("remote-b", "b.jpg")
        };
        var evidence = new[]
        {
            CreateEvidence("remote-a", "first"),
            CreateEvidence("remote-a", "duplicate")
        };

        var act = () => RestoreCompleted(files, evidence);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsMismatchedPersistedVerificationEvidence()
    {
        var act = () => RestoreCompleted(
            [CreateReadyFile("remote-a", "a.jpg")],
            [CreateEvidence("REMOTE-A")]);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudVerificationResult_RejectsSuccessfulResultWithoutRemoteId(string? remoteId)
    {
        var act = () => new CloudVerificationResult(true, "verified", remoteId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudVerificationResult_RejectsBlankRemoteIdWhenProvided(string remoteId)
    {
        var act = () => new CloudVerificationResult(false, "not verified", remoteId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudVerificationResult_RejectsBlankReason(string? reason)
    {
        var act = () => new CloudVerificationResult(false, reason!, null);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudFileVerification_RejectsBlankRemoteId(string? remoteId)
    {
        var act = () => new CloudFileVerification(
            remoteId!,
            DateTimeOffset.UnixEpoch,
            true,
            "verified");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudFileVerification_RejectsBlankReason(string? reason)
    {
        var act = () => new CloudFileVerification(
            "remote-id",
            DateTimeOffset.UnixEpoch,
            true,
            reason!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsNullFiles()
    {
        var act = () => new CloudTransferJob(
            Guid.NewGuid(),
            CloudProviderKind.Quark,
            new CloudPath("/backup"),
            CloudTransferPriority.Required,
            CloudTransferState.Pending,
            null!,
            DateTimeOffset.UnixEpoch);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsNullFileEntry()
    {
        var act = () => CreateJob(files: [null!]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_RejectsEmptyFiles()
    {
        var act = () => new CloudTransferJob(
            Guid.NewGuid(),
            CloudProviderKind.Quark,
            new CloudPath("/backup"),
            CloudTransferPriority.Required,
            CloudTransferState.Pending,
            Array.Empty<CloudTransferFile>(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloudTransferJob_CopiesFilesAtConstruction()
    {
        var files = new List<CloudTransferFile>
        {
            new("C:/source.jpg", new CloudRelativePath("source.jpg"), 42, null)
        };
        var job = new CloudTransferJob(
            Guid.NewGuid(),
            CloudProviderKind.Baidu,
            new CloudPath("/backup"),
            CloudTransferPriority.Opportunistic,
            CloudTransferState.Pending,
            files,
            DateTimeOffset.UtcNow);

        files.Clear();

        job.Files.Should().ContainSingle();
        job.Files.Should().NotBeSameAs(files);
    }

    [Fact]
    public void CloudTransferJob_ExposesFilesAsReadOnly()
    {
        var job = new CloudTransferJob(
            Guid.NewGuid(),
            CloudProviderKind.Simulated,
            new CloudPath("/backup"),
            CloudTransferPriority.Required,
            CloudTransferState.Running,
            [new CloudTransferFile("C:/source.raw", new CloudRelativePath("source.raw"), 84, "hash")],
            DateTimeOffset.UtcNow);

        var files = job.Files.Should().BeAssignableTo<IList<CloudTransferFile>>().Subject;
        var act = () => files.Clear();

        act.Should().Throw<NotSupportedException>();
    }

    private static CloudObject CreateObject(
        CloudProviderKind provider = CloudProviderKind.Quark,
        CloudObjectKind kind = CloudObjectKind.Image,
        string remoteId = "remote-id",
        string name = "photo.jpg",
        long size = 42)
    {
        return new CloudObject(
            provider,
            remoteId,
            new CloudPath("/photo.jpg"),
            name,
            kind,
            size,
            DateTimeOffset.UnixEpoch,
            null,
            true);
    }

    private static CloudTransferFile CreateFile()
    {
        return new CloudTransferFile(
            "C:/photo.jpg",
            new CloudRelativePath("photo.jpg"),
            42,
            null);
    }

    private static CloudTransferFile CreateReadyFile(
        string remoteId = "remote-id",
        string fileName = "photo.jpg")
    {
        return new CloudTransferFile(
            $"C:/{fileName}",
            new CloudRelativePath(fileName),
            42,
            null,
            42,
            remoteId);
    }

    private static CloudFileVerification CreateEvidence(
        string remoteId,
        string reason = "verified",
        DateTimeOffset? verifiedAt = null)
    {
        return new CloudFileVerification(
            remoteId,
            verifiedAt ?? DateTimeOffset.UnixEpoch.AddHours(1),
            true,
            reason);
    }

    private static CloudTransferJob RestoreCompleted(
        IReadOnlyList<CloudTransferFile> files,
        IReadOnlyList<CloudFileVerification> evidence)
    {
        return new CloudTransferJob(
            Guid.NewGuid(),
            CloudProviderKind.Quark,
            new CloudPath("/backup"),
            CloudTransferPriority.Required,
            CloudTransferState.Completed,
            files,
            DateTimeOffset.UnixEpoch,
            evidence);
    }

    private static CloudTransferJob CreateJob(
        Guid? id = null,
        CloudProviderKind provider = CloudProviderKind.Quark,
        CloudTransferPriority priority = CloudTransferPriority.Required,
        CloudTransferState state = CloudTransferState.Pending,
        IReadOnlyList<CloudTransferFile>? files = null)
    {
        return new CloudTransferJob(
            id ?? Guid.NewGuid(),
            provider,
            new CloudPath("/backup"),
            priority,
            state,
            files ?? [CreateFile()],
            DateTimeOffset.UnixEpoch);
    }
}
