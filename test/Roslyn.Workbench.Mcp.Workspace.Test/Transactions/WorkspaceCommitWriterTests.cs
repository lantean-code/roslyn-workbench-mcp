using System.Runtime.Versioning;
using System.Security.Cryptography;
using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceCommitWriterTests
{
    private const UnixFileMode _originalUnixFileMode = UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead;

    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IFile> _file = new();
    private readonly Mock<IDirectory> _directory = new();
    private readonly Mock<IPath> _path = new();
    private readonly Mock<IAtomicFileWriter> _atomicWriter = new();
    private readonly Mock<ICommitRecoveryStore> _recoveryStore = new();
    private readonly Mock<IAtomicFileCommitter> _fileCommitter = new();
    private readonly Mock<IPhysicalPathContainment> _pathContainment = new();
    private readonly WorkspaceCommitWriter _target;

    public WorkspaceCommitWriterTests()
    {
        _fileSystem.SetupGet(item => item.File).Returns(_file.Object);
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path.Setup(item => item.GetDirectoryName(It.IsAny<string>())).Returns("/workspace");
        if (!OperatingSystem.IsWindows())
        {
            ConfigureUnixFileMode(_originalUnixFileMode);
        }

        _pathContainment
            .Setup(item => item.TryGetStrictlyContainedPath(
                It.IsAny<string>(),
                It.IsAny<string>(),
                out It.Ref<string>.IsAny))
            .Returns((string _, string candidate, out string containedPath) =>
            {
                containedPath = candidate;
                return true;
            });

        _target = new WorkspaceCommitWriter(
            _fileSystem.Object,
            _atomicWriter.Object,
            _recoveryStore.Object,
            _fileCommitter.Object,
            _pathContainment.Object);
    }

    [Fact]
    public async Task GIVEN_UnchangedOriginal_WHEN_Revalidating_THEN_ShouldComplete()
    {
        var original = new byte[] { 1, 2, 3 };
        var manifest = CreateManifest(CreateEntry(WorkspaceFileOperation.Replace, Hash(original), "INTENDED"));
        _file.Setup(item => item.Exists("/workspace/file.cs")).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync("/workspace/file.cs", It.IsAny<CancellationToken>())).ReturnsAsync(original);

        var result = await _target.RevalidateAsync(manifest, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_TargetPhysicallyEscapesWorkspace_WHEN_Revalidating_THEN_ShouldRejectCommit()
    {
        var manifest = CreateManifest(CreateEntry(
            WorkspaceFileOperation.Create,
            originalHash: null,
            intendedHash: "INTENDED"));

        _pathContainment
            .Setup(item => item.TryGetStrictlyContainedPath(
                manifest.WorkspaceRoot,
                "/workspace/file.cs",
                out It.Ref<string>.IsAny))
            .Returns(false);

        var result = await _target.RevalidateAsync(
            manifest,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("outside the workspace root");
        _file.Verify(item => item.Exists(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DivergentOriginal_WHEN_Revalidating_THEN_ShouldRejectCommit()
    {
        var manifest = CreateManifest(CreateEntry(WorkspaceFileOperation.Replace, "ORIGINAL", "INTENDED"));
        _file.Setup(item => item.Exists("/workspace/file.cs")).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync("/workspace/file.cs", It.IsAny<CancellationToken>())).ReturnsAsync([9]);

        var result = await _target.RevalidateAsync(manifest, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("changed before commit application");
    }

    [Fact]
    public async Task GIVEN_UnixPermissionsChanged_WHEN_Revalidating_THEN_ShouldRejectCommit()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var original = new byte[] { 1, 2, 3 };
        var entry = CreateEntry(WorkspaceFileOperation.Replace, Hash(original), "INTENDED");
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        ConfigureUnixFileMode(entry.TargetPath, UnixFileMode.UserRead);

        var result = await _target.RevalidateAsync(
            CreateManifest(entry),
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("permissions");
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_Revalidating_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.RevalidateAsync(
            CreateManifest(CreateEntry(WorkspaceFileOperation.Create, null, "INTENDED")),
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _file.Verify(item => item.Exists(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task GIVEN_TargetExistenceChanged_WHEN_Revalidating_THEN_ShouldRejectCommit(
        int operationValue,
        bool exists)
    {
        var operation = (WorkspaceFileOperation)operationValue;
        var entry = CreateEntry(operation, "ORIGINAL", "INTENDED");
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(exists);

        var result = await _target.RevalidateAsync(CreateManifest(entry), TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(entry.TargetPath);
    }

    [Fact]
    public async Task GIVEN_ExistingDeleteMarker_WHEN_Revalidating_THEN_ShouldRejectCommit()
    {
        var entry = CreateEntry(WorkspaceFileOperation.Delete, "ORIGINAL", null);
        _file.Setup(item => item.Exists(entry.GetRequiredDeleteMarkerPath())).Returns(true);

        var result = await _target.RevalidateAsync(CreateManifest(entry), TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("delete marker");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task GIVEN_IntendedCreateOrReplaceState_WHEN_ValidatingAppliedState_THEN_ShouldComplete(
        int operationValue)
    {
        var operation = (WorkspaceFileOperation)operationValue;
        var intended = new byte[] { 1, 2, 3 };
        var entry = CreateEntry(operation, "ORIGINAL", Hash(intended));
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None))
            .ReturnsAsync(intended);

        var manifest = CreateManifest(entry);
        var result = await _target.ValidateAppliedStateAsync(manifest);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task GIVEN_IntendedDeleteState_WHEN_ValidatingAppliedState_THEN_ShouldRequireValidMarkerOnly(
        bool targetExists,
        bool markerExists)
    {
        var original = new byte[] { 1, 2, 3 };
        var entry = CreateEntry(WorkspaceFileOperation.Delete, Hash(original), null);
        var markerPath = entry.GetRequiredDeleteMarkerPath();
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(targetExists);
        _file.Setup(item => item.Exists(markerPath)).Returns(markerExists);
        _file.Setup(item => item.ReadAllBytesAsync(markerPath, CancellationToken.None)).ReturnsAsync(original);

        var manifest = CreateManifest(entry);
        var result = await _target.ValidateAppliedStateAsync(manifest);

        var expectedValidity = !targetExists && markerExists;
        result.IsValid.Should().Be(expectedValidity);
        if (result.IsValid)
        {
            result.ErrorMessage.Should().BeNull();
        }
        else
        {
            result.ErrorMessage.Should().Contain("changed after commit application");
        }
    }

    [Fact]
    public async Task GIVEN_CorruptedDeleteMarker_WHEN_ValidatingAppliedState_THEN_ShouldRejectCommit()
    {
        var original = new byte[] { 1, 2, 3 };
        var entry = CreateEntry(WorkspaceFileOperation.Delete, Hash(original), null);
        var markerPath = entry.GetRequiredDeleteMarkerPath();
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(false);
        _file.Setup(item => item.Exists(markerPath)).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(markerPath, CancellationToken.None)).ReturnsAsync([9, 9, 9]);

        var result = await _target.ValidateAppliedStateAsync(CreateManifest(entry));

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("delete marker");
    }

    [Fact]
    public async Task GIVEN_DeleteMarkerPermissionsChanged_WHEN_ValidatingAppliedState_THEN_ShouldRejectCommit()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var original = new byte[] { 1, 2, 3 };
        var entry = CreateEntry(WorkspaceFileOperation.Delete, Hash(original), null);
        var markerPath = entry.GetRequiredDeleteMarkerPath();
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(false);
        _file.Setup(item => item.Exists(markerPath)).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(markerPath, CancellationToken.None)).ReturnsAsync(original);
        ConfigureUnixFileMode(markerPath, UnixFileMode.UserRead);

        var result = await _target.ValidateAppliedStateAsync(CreateManifest(entry));

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("permissions");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task GIVEN_CreateOrReplaceStateDrift_WHEN_ValidatingAppliedState_THEN_ShouldRejectCommit(
        bool targetExists,
        bool hashMatches)
    {
        var intended = new byte[] { 1, 2, 3 };
        var actual = hashMatches ? intended : new byte[] { 4, 5, 6 };
        var entry = CreateEntry(WorkspaceFileOperation.Replace, "ORIGINAL", Hash(intended));
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(targetExists);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None))
            .ReturnsAsync(actual);

        var manifest = CreateManifest(entry);
        var result = await _target.ValidateAppliedStateAsync(manifest);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("changed after commit application");
    }

    [Fact]
    public async Task GIVEN_AppliedReplacementPermissionsDrift_WHEN_ValidatingAppliedState_THEN_ShouldRejectCommit()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var intended = new byte[] { 1, 2, 3 };
        var entry = CreateEntry(WorkspaceFileOperation.Replace, "ORIGINAL", Hash(intended));
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None)).ReturnsAsync(intended);
        ConfigureUnixFileMode(entry.TargetPath, UnixFileMode.UserRead);

        var result = await _target.ValidateAppliedStateAsync(CreateManifest(entry));

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("permissions");
    }

    [Fact]
    public async Task GIVEN_TargetPhysicallyEscapesWorkspace_WHEN_ValidatingAppliedState_THEN_ShouldRejectCommit()
    {
        var entry = CreateEntry(WorkspaceFileOperation.Create, null, "INTENDED");
        var manifest = CreateManifest(entry);
        _pathContainment
            .Setup(item => item.TryGetStrictlyContainedPath(
                manifest.WorkspaceRoot,
                entry.TargetPath,
                out It.Ref<string>.IsAny))
            .Returns(false);

        var result = await _target.ValidateAppliedStateAsync(manifest);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("outside the workspace root");
        _file.Verify(item => item.Exists(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CreateReplaceAndDelete_WHEN_Applying_THEN_ShouldUseDurableArtifactsWithoutCancellation()
    {
        var beforeReplace = new byte[] { 4 };
        var beforeDelete = new byte[] { 5 };
        var entries = new[]
        {
            CreateEntry(WorkspaceFileOperation.Create, null, Hash([1]), "/workspace/a.cs", "staged/a.bin"),
            CreateEntry(WorkspaceFileOperation.Replace, Hash(beforeReplace), Hash([2]), "/workspace/b.cs", "staged/b.bin"),
            CreateEntry(WorkspaceFileOperation.Delete, Hash(beforeDelete), null, "/workspace/d.cs", null),
        };

        var manifest = CreateManifest(entries);
        _recoveryStore.Setup(item => item.ReadArtifactAsync("commit", "staged/a.bin", CancellationToken.None)).ReturnsAsync([1]);
        _recoveryStore.Setup(item => item.ReadArtifactAsync("commit", "staged/b.bin", CancellationToken.None)).ReturnsAsync([2]);
        _file.Setup(item => item.Exists("/workspace/a.cs")).Returns(false);
        _file.Setup(item => item.Exists("/workspace/b.cs")).Returns(true);
        _file.Setup(item => item.Exists("/workspace/d.cs")).Returns(true);
        _file.Setup(item => item.Exists("/workspace/d.cs.delete")).Returns(false);
        _file.Setup(item => item.ReadAllBytesAsync("/workspace/b.cs", CancellationToken.None)).ReturnsAsync(beforeReplace);
        _file.Setup(item => item.ReadAllBytesAsync("/workspace/d.cs", CancellationToken.None)).ReturnsAsync(beforeDelete);

        var result = await _target.ApplyAsync(manifest);

        result.IsValid.Should().BeTrue();
        _directory.Verify(item => item.CreateDirectory("/workspace/new"), Times.Once);
        _atomicWriter.Verify(item => item.WriteAllBytesAsync(
            "/workspace/a.cs",
            It.Is<ReadOnlyMemory<byte>>(bytes => bytes.ToArray()[0] == 1),
            AtomicFileAccess.Default,
            null,
            CancellationToken.None), Times.Once);

        _atomicWriter.Verify(item => item.WriteAllBytesAsync(
            "/workspace/b.cs",
            It.Is<ReadOnlyMemory<byte>>(bytes => bytes.ToArray()[0] == 2),
            AtomicFileAccess.Default,
            OperatingSystem.IsWindows() ? null : _originalUnixFileMode,
            CancellationToken.None), Times.Once);
        _fileCommitter.Verify(item => item.Move("/workspace/d.cs", "/workspace/d.cs.delete"), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task GIVEN_CorruptedStagedArtifact_WHEN_ApplyingCreateOrReplace_THEN_ShouldRejectBeforeWriting(int operationValue)
    {
        var operation = (WorkspaceFileOperation)operationValue;
        var original = new byte[] { 1 };
        var entry = CreateEntry(
            operation,
            operation == WorkspaceFileOperation.Replace ? Hash(original) : null,
            Hash([2]));

        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(operation == WorkspaceFileOperation.Replace);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None)).ReturnsAsync(original);
        _recoveryStore.Setup(item => item.ReadArtifactAsync("commit", entry.GetRequiredStagedPath(), CancellationToken.None)).ReturnsAsync([9]);

        var result = await _target.ApplyAsync(CreateManifest(entry));

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("staged recovery artifact");
        _atomicWriter.Verify(
            item => item.WriteAllBytesAsync(
                It.IsAny<string>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<AtomicFileAccess>(),
                It.IsAny<UnixFileMode?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_DeleteMarkerAppearsAfterRevalidation_WHEN_Applying_THEN_ShouldRejectDelete()
    {
        var original = new byte[] { 1 };
        var entry = CreateEntry(WorkspaceFileOperation.Delete, Hash(original), null);
        _file.SetupSequence(item => item.Exists(entry.GetRequiredDeleteMarkerPath()))
            .Returns(false)
            .Returns(true);

        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None)).ReturnsAsync(original);

        var result = await _target.ApplyAsync(CreateManifest(entry));

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("delete marker");
        _fileCommitter.Verify(item => item.Move(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_TargetDriftsBeforeEntryApplication_WHEN_Applying_THEN_ShouldStopBeforeWriting()
    {
        var entry = CreateEntry(WorkspaceFileOperation.Create, null, "INTENDED");
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(true);

        var result = await _target.ApplyAsync(CreateManifest(entry));

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(entry.TargetPath);
        _atomicWriter.Verify(
            item => item.WriteAllBytesAsync(
                It.IsAny<string>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<AtomicFileAccess>(),
                It.IsAny<UnixFileMode?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_AppliedTargets_WHEN_Restoring_THEN_ShouldRestoreOriginalAndDeleteCreatedFile()
    {
        var intended = new byte[] { 8 };
        var original = new byte[] { 3 };
        var entries = new[]
        {
            CreateEntry(WorkspaceFileOperation.Create, null, Hash(intended), "/workspace/a.cs", "staged/a.bin"),
            CreateEntry(WorkspaceFileOperation.Replace, Hash(original), Hash(intended), "/workspace/b.cs", "staged/b.bin", "backup/b.bin"),
        };

        var manifest = CreateManifest(entries);
        _file.SetupSequence(item => item.Exists("/workspace/a.cs"))
            .Returns(true)
            .Returns(false);

        _file.SetupSequence(item => item.Exists("/workspace/b.cs"))
            .Returns(true)
            .Returns(true);

        _file.Setup(item => item.ReadAllBytesAsync("/workspace/a.cs", CancellationToken.None)).ReturnsAsync(intended);
        _file.SetupSequence(item => item.ReadAllBytesAsync("/workspace/b.cs", CancellationToken.None))
            .ReturnsAsync(intended)
            .ReturnsAsync(original);

        _recoveryStore.Setup(item => item.ReadArtifactAsync("commit", "backup/b.bin", CancellationToken.None)).ReturnsAsync(original);
        _directory.Setup(item => item.Exists("/workspace/new")).Returns(true);
        _directory.Setup(item => item.EnumerateFileSystemEntries("/workspace/new")).Returns([]);

        var result = await _target.RestoreAsync(manifest);

        result.Should().Be(RecoveryState.Restored);
        _atomicWriter.Verify(item => item.WriteAllBytesAsync(
            "/workspace/b.cs",
            It.IsAny<ReadOnlyMemory<byte>>(),
            AtomicFileAccess.Default,
            OperatingSystem.IsWindows() ? null : _originalUnixFileMode,
            CancellationToken.None), Times.Once);
        _file.Verify(item => item.Delete("/workspace/a.cs"), Times.Once);
        _directory.Verify(item => item.Delete("/workspace/new"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ExternallyDivergentTarget_WHEN_Restoring_THEN_ShouldPreserveItAndReportConflict()
    {
        var entry = CreateEntry(WorkspaceFileOperation.Create, null, "INTENDED");
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None)).ReturnsAsync([7]);
        _directory.Setup(item => item.EnumerateFileSystemEntries("/workspace/new")).Returns([entry.TargetPath]);

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(RecoveryState.RecoveryConflict);
        _file.Verify(item => item.Delete(entry.TargetPath), Times.Never);
    }

    [Theory]
    [InlineData("markerOnly", RecoveryState.Restored)]
    [InlineData("markerAndOriginal", RecoveryState.Restored)]
    [InlineData("originalOnly", RecoveryState.Restored)]
    [InlineData("missing", RecoveryState.RecoveryConflict)]
    [InlineData("divergent", RecoveryState.RecoveryConflict)]
    public async Task GIVEN_DeletedOriginalState_WHEN_Restoring_THEN_ShouldRestoreOrReportConflict(
        string scenario,
        RecoveryState expected)
    {
        var original = new byte[] { 1 };
        var entry = CreateEntry(WorkspaceFileOperation.Delete, Hash(original), null);
        var markerPath = entry.GetRequiredDeleteMarkerPath();
        var markerExists = scenario is "markerOnly" or "markerAndOriginal" or "divergent";
        var targetExists = scenario is "markerAndOriginal" or "originalOnly" or "divergent";
        if (scenario == "markerOnly")
        {
            _file.SetupSequence(item => item.Exists(markerPath))
                .Returns(true)
                .Returns(false);

            _file.SetupSequence(item => item.Exists(entry.TargetPath))
                .Returns(false)
                .Returns(true);
        }
        else if (scenario == "markerAndOriginal")
        {
            _file.SetupSequence(item => item.Exists(markerPath))
                .Returns(true)
                .Returns(false);

            _file.Setup(item => item.Exists(entry.TargetPath)).Returns(true);
        }
        else
        {
            _file.Setup(item => item.Exists(markerPath)).Returns(markerExists);
            _file.Setup(item => item.Exists(entry.TargetPath)).Returns(targetExists);
        }

        _file.Setup(item => item.ReadAllBytesAsync(markerPath, CancellationToken.None)).ReturnsAsync(original);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None))
            .ReturnsAsync(scenario == "divergent" ? new byte[] { 9 } : original);

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(expected);
        Times expectedMoves;
        if (scenario == "markerOnly")
        {
            expectedMoves = Times.Once();
        }
        else
        {
            expectedMoves = Times.Never();
        }

        _fileCommitter.Verify(
            item => item.Move(markerPath, entry.TargetPath),
            expectedMoves);

        Times expectedDeletes;
        if (scenario == "markerAndOriginal")
        {
            expectedDeletes = Times.Once();
        }
        else
        {
            expectedDeletes = Times.Never();
        }

        _file.Verify(
            item => item.Delete(markerPath),
            expectedDeletes);
    }

    [Fact]
    public async Task GIVEN_CorruptedDeleteMarker_WHEN_Restoring_THEN_ShouldRetainMarkerAndReportConflict()
    {
        var original = new byte[] { 1 };
        var entry = CreateEntry(WorkspaceFileOperation.Delete, Hash(original), null);
        var markerPath = entry.GetRequiredDeleteMarkerPath();
        _file.Setup(item => item.Exists(markerPath)).Returns(true);
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(false);
        _file.Setup(item => item.ReadAllBytesAsync(markerPath, CancellationToken.None)).ReturnsAsync([9]);

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(RecoveryState.RecoveryConflict);
        _fileCommitter.Verify(item => item.Move(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _file.Verify(item => item.Delete(markerPath), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DeleteMarkerPermissionsChanged_WHEN_Restoring_THEN_ShouldRetainMarkerAndReportConflict()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var original = new byte[] { 1 };
        var entry = CreateEntry(WorkspaceFileOperation.Delete, Hash(original), null);
        var markerPath = entry.GetRequiredDeleteMarkerPath();
        _file.Setup(item => item.Exists(markerPath)).Returns(true);
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(false);
        _file.Setup(item => item.ReadAllBytesAsync(markerPath, CancellationToken.None)).ReturnsAsync(original);
        ConfigureUnixFileMode(markerPath, UnixFileMode.UserRead);

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(RecoveryState.RecoveryConflict);
        _fileCommitter.Verify(item => item.Move(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CorruptedBackup_WHEN_RestoringAppliedReplacement_THEN_ShouldPreserveTargetAndReportConflict()
    {
        var original = new byte[] { 1 };
        var intended = new byte[] { 2 };
        var entry = CreateEntry(WorkspaceFileOperation.Replace, Hash(original), Hash(intended));
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None)).ReturnsAsync(intended);
        _recoveryStore.Setup(item => item.ReadArtifactAsync("commit", entry.GetRequiredBackupPath(), CancellationToken.None)).ReturnsAsync([9]);

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(RecoveryState.RecoveryConflict);
        _atomicWriter.Verify(
            item => item.WriteAllBytesAsync(
                It.IsAny<string>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<AtomicFileAccess>(),
                It.IsAny<UnixFileMode?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_RestoredReplacementCannotBeCertified_WHEN_Restoring_THEN_ShouldReportConflict()
    {
        var original = new byte[] { 1 };
        var intended = new byte[] { 2 };
        var entry = CreateEntry(WorkspaceFileOperation.Replace, Hash(original), Hash(intended));
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None)).ReturnsAsync(intended);
        _recoveryStore.Setup(item => item.ReadArtifactAsync("commit", entry.GetRequiredBackupPath(), CancellationToken.None)).ReturnsAsync(original);

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(RecoveryState.RecoveryConflict);
        _atomicWriter.Verify(
            item => item.WriteAllBytesAsync(
                entry.TargetPath,
                It.IsAny<ReadOnlyMemory<byte>>(),
                AtomicFileAccess.Default,
                OperatingSystem.IsWindows() ? null : _originalUnixFileMode,
                CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [InlineData("original", RecoveryState.Restored)]
    [InlineData("missing", RecoveryState.RecoveryConflict)]
    [InlineData("divergent", RecoveryState.RecoveryConflict)]
    public async Task GIVEN_ReplacedOriginalState_WHEN_Restoring_THEN_ShouldPreserveOriginalOrReportConflict(
        string scenario,
        RecoveryState expected)
    {
        var original = new byte[] { 1 };
        var entry = CreateEntry(WorkspaceFileOperation.Replace, Hash(original), "INTENDED");
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(scenario != "missing");
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None))
            .ReturnsAsync(scenario == "original" ? original : new byte[] { 9 });

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(expected);
        _recoveryStore.Verify(
            item => item.ReadArtifactAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_CreatedTargetIsMissing_WHEN_Restoring_THEN_ShouldRemainRestored()
    {
        var entry = CreateEntry(WorkspaceFileOperation.Create, null, "INTENDED");
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(false);

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(RecoveryState.Restored);
        _file.Verify(item => item.Delete(entry.TargetPath), Times.Never);
    }

    [Fact]
    public async Task GIVEN_AppliedReplacementWithoutParentDirectory_WHEN_Restoring_THEN_ShouldRejectInvalidTarget()
    {
        var original = new byte[] { 1 };
        var intended = new byte[] { 8 };
        var entry = CreateEntry(WorkspaceFileOperation.Replace, Hash(original), Hash(intended), "File.cs");
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None)).ReturnsAsync(intended);
        _recoveryStore.Setup(item => item.ReadArtifactAsync("commit", entry.GetRequiredBackupPath(), CancellationToken.None)).ReturnsAsync(original);
        _path.Setup(item => item.GetDirectoryName(entry.TargetPath)).Returns((string?)null);

        var action = async () => await _target.RestoreAsync(CreateManifest(entry));

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GIVEN_AppliedReplacementPermissionsDrift_WHEN_Restoring_THEN_ShouldPreserveTargetAndReportConflict()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var intended = new byte[] { 8 };
        var entry = CreateEntry(WorkspaceFileOperation.Replace, "ORIGINAL", Hash(intended));
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(entry.TargetPath, CancellationToken.None)).ReturnsAsync(intended);
        ConfigureUnixFileMode(entry.TargetPath, UnixFileMode.UserRead);

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(RecoveryState.RecoveryConflict);
        _atomicWriter.Verify(item => item.WriteAllBytesAsync(
            It.IsAny<string>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<AtomicFileAccess>(),
            It.IsAny<UnixFileMode?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("nonempty")]
    public async Task GIVEN_DirectoryCannotBeRemoved_WHEN_Restoring_THEN_ShouldLeaveDirectory(string scenario)
    {
        var manifest = CreateManifest();
        _directory.Setup(item => item.Exists("/workspace/new")).Returns(scenario != "missing");
        _directory.Setup(item => item.EnumerateFileSystemEntries("/workspace/new"))
            .Returns(scenario == "nonempty" ? ["/workspace/new/File.cs"] : []);

        var result = await _target.RestoreAsync(manifest);

        result.Should().Be(RecoveryState.Restored);
        _directory.Verify(item => item.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_RestorationIoFailure_WHEN_Restoring_THEN_ShouldReportIncomplete()
    {
        var original = new byte[] { 1 };
        var entry = CreateEntry(WorkspaceFileOperation.Delete, Hash(original), null);
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(false);
        _file.Setup(item => item.Exists(entry.GetRequiredDeleteMarkerPath())).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(entry.GetRequiredDeleteMarkerPath(), CancellationToken.None)).ReturnsAsync(original);
        _fileCommitter.Setup(item => item.Move(entry.GetRequiredDeleteMarkerPath(), entry.TargetPath)).Throws(new IOException());

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(RecoveryState.RecoveryIncomplete);
    }

    [Fact]
    public async Task GIVEN_RestorationAccessFailure_WHEN_Restoring_THEN_ShouldReportIncomplete()
    {
        var original = new byte[] { 1 };
        var entry = CreateEntry(WorkspaceFileOperation.Delete, Hash(original), null);
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(false);
        _file.Setup(item => item.Exists(entry.GetRequiredDeleteMarkerPath())).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(entry.GetRequiredDeleteMarkerPath(), CancellationToken.None)).ReturnsAsync(original);
        _fileCommitter.Setup(item => item.Move(entry.GetRequiredDeleteMarkerPath(), entry.TargetPath))
            .Throws(new UnauthorizedAccessException());

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(RecoveryState.RecoveryIncomplete);
    }

    [Theory]
    [InlineData("deleted", true)]
    [InlineData("missing", true)]
    [InlineData("io", false)]
    [InlineData("access", false)]
    public async Task GIVEN_CommittedDelete_WHEN_Completing_THEN_ShouldRemoveMarkerOrRetainJournal(
        string scenario,
        bool expected)
    {
        var entry = CreateEntry(WorkspaceFileOperation.Delete, "ORIGINAL", null);
        var markerPath = entry.GetRequiredDeleteMarkerPath();
        _file.Setup(item => item.Exists(markerPath)).Returns(scenario != "missing");
        if (scenario == "io")
        {
            _file.Setup(item => item.Delete(markerPath)).Throws(new IOException());
        }
        else if (scenario == "access")
        {
            _file.Setup(item => item.Delete(markerPath)).Throws(new UnauthorizedAccessException());
        }

        var result = await _target.CompleteAsync(CreateManifest(entry));

        result.Should().Be(expected);
    }

    private static WorkspaceCommitManifest CreateManifest(params WorkspaceCommitEntry[] entries)
    {
        return new()
        {
            CommitId = "commit",
            LoadedPath = "/workspace/workspace.slnx",
            WorkspaceRoot = "/workspace",
            State = RecoveryState.Applying,
            Entries = entries,
            CreatedDirectories = ["/workspace/new"],
        };
    }

    [UnsupportedOSPlatform("windows")]
    private void ConfigureUnixFileMode(UnixFileMode mode)
    {
        _file.Setup(item => item.GetUnixFileMode(It.IsAny<string>())).Returns(mode);
    }

    [UnsupportedOSPlatform("windows")]
    private void ConfigureUnixFileMode(string path, UnixFileMode mode)
    {
        _file.Setup(item => item.GetUnixFileMode(path)).Returns(mode);
    }

    private static WorkspaceCommitEntry CreateEntry(
        WorkspaceFileOperation operation,
        string? originalHash,
        string? intendedHash,
        string target = "/workspace/file.cs",
        string? staged = "staged/file.bin",
        string? backup = "backup/file.bin")
    {
        return new()
        {
            TargetPath = target,
            Operation = operation,
            OriginalExists = operation != WorkspaceFileOperation.Create,
            OriginalHash = originalHash,
            IntendedHash = intendedHash,
            OriginalUnixFileMode = operation != WorkspaceFileOperation.Create && !OperatingSystem.IsWindows()
                ? _originalUnixFileMode
                : null,
            StagedPath = staged,
            BackupPath = backup,
            DeleteMarkerPath = operation == WorkspaceFileOperation.Delete ? $"{target}.delete" : null,
        };
    }

    private static string Hash(byte[] contents)
    {
        return Convert.ToHexString(SHA256.HashData(contents));
    }
}
