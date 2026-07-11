using System.Security.Cryptography;
using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceCommitWriterTests
{
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IFile> _file = new();
    private readonly Mock<IDirectory> _directory = new();
    private readonly Mock<IPath> _path = new();
    private readonly Mock<IAtomicFileWriter> _atomicWriter = new();
    private readonly Mock<ICommitRecoveryStore> _recoveryStore = new();
    private readonly Mock<IAtomicFileCommitter> _fileCommitter = new();
    private readonly WorkspaceCommitWriter _target;

    public WorkspaceCommitWriterTests()
    {
        _fileSystem.SetupGet(item => item.File).Returns(_file.Object);
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path.Setup(item => item.GetDirectoryName(It.IsAny<string>())).Returns("/workspace");
        _target = new WorkspaceCommitWriter(_fileSystem.Object, _atomicWriter.Object, _recoveryStore.Object, _fileCommitter.Object);
    }

    [Fact]
    public async Task GIVEN_UnchangedOriginal_WHEN_Revalidating_THEN_ShouldComplete()
    {
        var original = new byte[] { 1, 2, 3 };
        var manifest = CreateManifest(CreateEntry(WorkspaceFileOperation.Replace, Hash(original), "INTENDED"));
        _file.Setup(item => item.Exists("/workspace/file.cs")).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync("/workspace/file.cs", It.IsAny<CancellationToken>())).ReturnsAsync(original);

        var action = async () => await _target.RevalidateAsync(manifest, TestContext.Current.CancellationToken);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GIVEN_DivergentOriginal_WHEN_Revalidating_THEN_ShouldRejectCommit()
    {
        var manifest = CreateManifest(CreateEntry(WorkspaceFileOperation.Replace, "ORIGINAL", "INTENDED"));
        _file.Setup(item => item.Exists("/workspace/file.cs")).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync("/workspace/file.cs", It.IsAny<CancellationToken>())).ReturnsAsync([9]);

        var action = async () => await _target.RevalidateAsync(manifest, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task GIVEN_CreateReplaceAndDelete_WHEN_Applying_THEN_ShouldUseDurableArtifactsWithoutCancellation()
    {
        var beforeReplace = new byte[] { 4 };
        var beforeDelete = new byte[] { 5 };
        var entries = new[]
        {
            CreateEntry(WorkspaceFileOperation.Create, null, "A", "/workspace/a.cs", "staged/a.bin"),
            CreateEntry(WorkspaceFileOperation.Replace, Hash(beforeReplace), "C", "/workspace/b.cs", "staged/b.bin"),
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

        await _target.ApplyAsync(manifest);

        _directory.Verify(item => item.CreateDirectory("/workspace/new"), Times.Once);
        _atomicWriter.Verify(item => item.WriteAllBytesAsync("/workspace/a.cs", It.Is<ReadOnlyMemory<byte>>(bytes => bytes.ToArray()[0] == 1), CancellationToken.None), Times.Once);
        _atomicWriter.Verify(item => item.WriteAllBytesAsync("/workspace/b.cs", It.Is<ReadOnlyMemory<byte>>(bytes => bytes.ToArray()[0] == 2), CancellationToken.None), Times.Once);
        _fileCommitter.Verify(item => item.Move("/workspace/d.cs", "/workspace/d.cs.delete"), Times.Once);
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
        _file.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _file.Setup(item => item.ReadAllBytesAsync(It.IsAny<string>(), CancellationToken.None)).ReturnsAsync(intended);
        _recoveryStore.Setup(item => item.ReadArtifactAsync("commit", "backup/b.bin", CancellationToken.None)).ReturnsAsync(original);
        _directory.Setup(item => item.Exists("/workspace/new")).Returns(true);
        _directory.Setup(item => item.EnumerateFileSystemEntries("/workspace/new")).Returns([]);

        var result = await _target.RestoreAsync(manifest);

        result.Should().Be(RecoveryState.Restored);
        _atomicWriter.Verify(item => item.WriteAllBytesAsync("/workspace/b.cs", It.IsAny<ReadOnlyMemory<byte>>(), CancellationToken.None), Times.Once);
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

    [Fact]
    public async Task GIVEN_RestorationIoFailure_WHEN_Restoring_THEN_ShouldReportIncomplete()
    {
        var entry = CreateEntry(WorkspaceFileOperation.Delete, "ORIGINAL", null);
        _file.Setup(item => item.Exists(entry.TargetPath)).Returns(false);
        _file.Setup(item => item.Exists(entry.DeleteMarkerPath!)).Returns(true);
        _fileCommitter.Setup(item => item.Move(entry.DeleteMarkerPath!, entry.TargetPath)).Throws(new IOException());

        var result = await _target.RestoreAsync(CreateManifest(entry));

        result.Should().Be(RecoveryState.RecoveryIncomplete);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_CommittedDelete_WHEN_Completing_THEN_ShouldRemoveMarkerOrRetainJournal(bool deleteSucceeds)
    {
        var entry = CreateEntry(WorkspaceFileOperation.Delete, "ORIGINAL", null);
        _file.Setup(item => item.Exists(entry.DeleteMarkerPath!)).Returns(true);
        if (!deleteSucceeds)
        {
            _file.Setup(item => item.Delete(entry.DeleteMarkerPath!)).Throws(new IOException());
        }

        var result = await _target.CompleteAsync(CreateManifest(entry));

        result.Should().Be(deleteSucceeds);
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
