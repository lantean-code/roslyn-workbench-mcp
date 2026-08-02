using System.Diagnostics;
using System.Security.Cryptography;
using Moq;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class DurableWorkspaceCommitIntegrationTests : IDisposable
{
    private const UnixFileMode _replaceUnixFileMode = UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead;

    private static readonly TimeSpan _processTimeout = TimeSpan.FromSeconds(10);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-durable-commit-tests", Guid.NewGuid().ToString("n"));
    private readonly string _stateDirectory;
    private readonly IFileSystem _fileSystem = new FileSystem();
    private readonly AtomicFileWriter _atomicWriter;
    private readonly IAtomicFileCommitter _fileCommitter;
    private readonly IPhysicalPathContainment _pathContainment;
    private readonly CommitRecoveryStore _store;
    private readonly WorkspaceCommitWriter _writer;

    public DurableWorkspaceCommitIntegrationTests()
    {
        _stateDirectory = Path.Combine(_root, "state");
        Directory.CreateDirectory(_root);
        _fileCommitter = new NativeAtomicFileCommitter();
        _atomicWriter = new AtomicFileWriter(_fileSystem, _fileCommitter);
        _pathContainment = CreatePathContainment(_fileSystem);
        var stateDirectorySecurity = new WorkspaceStateDirectorySecurity(_fileSystem);
        var stateDirectory = new WorkspaceStateDirectory(
            Options.Create(new WorkspaceOptions { StateDirectory = _stateDirectory }),
            _fileSystem,
            stateDirectorySecurity);

        stateDirectory.Initialize();
        _store = new CommitRecoveryStore(
            _fileSystem,
            _atomicWriter,
            new WorkspacePathComparison(),
            _pathContainment,
            stateDirectory,
            stateDirectorySecurity);

        _writer = new WorkspaceCommitWriter(
            _fileSystem,
            _atomicWriter,
            _store,
            _fileCommitter,
            _pathContainment);
    }

    [Theory]
    [InlineData(RecoveryState.Prepared, false)]
    [InlineData(RecoveryState.Applying, true)]
    [InlineData(RecoveryState.RecoveryIncomplete, true)]
    public async Task GIVEN_InterruptedCommit_WHEN_FreshRecoveryRuns_THEN_ShouldRestoreExactOriginalState(
        RecoveryState state,
        bool applyBeforeRestart)
    {
        var transaction = await CreateTransactionAsync(state);
        if (applyBeforeRestart)
        {
            await _writer.ApplyAsync(transaction.Manifest);
        }

        await CreateFreshRecoveryService().RecoverAsync(TestContext.Current.CancellationToken);

        (await File.ReadAllBytesAsync(transaction.ReplacePath, TestContext.Current.CancellationToken)).Should().Equal(transaction.ReplaceOriginal);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(transaction.ReplacePath).Should().Be(_replaceUnixFileMode);
        }

        File.Exists(transaction.CreatePath).Should().BeFalse();
        (await File.ReadAllBytesAsync(transaction.DeletePath, TestContext.Current.CancellationToken)).Should().Equal(transaction.DeleteOriginal);
        Directory.Exists(transaction.CreatedDirectory).Should().BeFalse();
        (await _store.GetManifestsAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_DurablyCommittedState_WHEN_FreshRecoveryRuns_THEN_ShouldKeepIntendedStateAndFinishCleanup()
    {
        var transaction = await CreateTransactionAsync(RecoveryState.Committed);
        await _writer.ApplyAsync(transaction.Manifest);

        await CreateFreshRecoveryService().RecoverAsync(TestContext.Current.CancellationToken);

        (await File.ReadAllBytesAsync(transaction.ReplacePath, TestContext.Current.CancellationToken)).Should().Equal(transaction.ReplaceIntended);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(transaction.ReplacePath).Should().Be(_replaceUnixFileMode);
        }

        (await File.ReadAllBytesAsync(transaction.CreatePath, TestContext.Current.CancellationToken)).Should().Equal(transaction.CreateIntended);
        File.Exists(transaction.DeletePath).Should().BeFalse();
        File.Exists(transaction.DeleteMarkerPath).Should().BeFalse();
        (await _store.GetManifestsAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_ExternalDivergenceAfterPartialCommit_WHEN_Recovering_THEN_ShouldPreserveDivergenceAndRestoreOtherTargets()
    {
        var transaction = await CreateTransactionAsync(RecoveryState.Applying);
        await _writer.ApplyAsync(transaction.Manifest);
        var divergent = new byte[] { 99, 0, 98, 0, 97 };
        await File.WriteAllBytesAsync(transaction.ReplacePath, divergent, TestContext.Current.CancellationToken);

        await CreateFreshRecoveryService().RecoverAsync(TestContext.Current.CancellationToken);

        (await File.ReadAllBytesAsync(transaction.ReplacePath, TestContext.Current.CancellationToken)).Should().Equal(divergent);
        File.Exists(transaction.CreatePath).Should().BeFalse();
        (await File.ReadAllBytesAsync(transaction.DeletePath, TestContext.Current.CancellationToken)).Should().Equal(transaction.DeleteOriginal);
        var manifests = await _store.GetManifestsAsync(TestContext.Current.CancellationToken);
        manifests.Should().ContainSingle().Which.State.Should().Be(RecoveryState.RecoveryConflict);
    }

    [Fact]
    public async Task GIVEN_DeterministicFailureDuringSecondTarget_WHEN_Restoring_THEN_ShouldReverseFirstTargetExactly()
    {
        var transaction = await CreateTransactionAsync(RecoveryState.Applying);
        var writes = 0;
        var faultingAtomicWriter = new Mock<IAtomicFileWriter>();
        faultingAtomicWriter.Setup(item => item.WriteAllBytesAsync(
                It.IsAny<string>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<AtomicFileAccess>(),
                It.IsAny<UnixFileMode?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string path,
                ReadOnlyMemory<byte> contents,
                AtomicFileAccess access,
                UnixFileMode? unixFileMode,
                CancellationToken cancellationToken) =>
            {
                writes++;
                if (writes == 2)
                {
                    return ValueTask.FromException(new IOException("Injected second-target failure."));
                }

                return _atomicWriter.WriteAllBytesAsync(
                    path,
                    contents,
                    access,
                    unixFileMode,
                    cancellationToken);
            });

        var writer = new WorkspaceCommitWriter(
            _fileSystem,
            faultingAtomicWriter.Object,
            _store,
            _fileCommitter,
            _pathContainment);

        var apply = async () => await writer.ApplyAsync(transaction.Manifest);
        await apply.Should().ThrowAsync<IOException>();
        var recoveryState = await writer.RestoreAsync(transaction.Manifest);

        recoveryState.Should().Be(RecoveryState.Restored);
        (await File.ReadAllBytesAsync(transaction.ReplacePath, TestContext.Current.CancellationToken)).Should().Equal(transaction.ReplaceOriginal);
        File.Exists(transaction.CreatePath).Should().BeFalse();
        (await File.ReadAllBytesAsync(transaction.DeletePath, TestContext.Current.CancellationToken)).Should().Equal(transaction.DeleteOriginal);
    }

    [Fact]
    public async Task GIVEN_RecoveryArtifactPathEscapesCommitDirectory_WHEN_Reading_THEN_ShouldRejectTraversal()
    {
        var action = async () => await _store.ReadArtifactAsync(
            "commit",
            $"..{Path.DirectorySeparatorChar}outside.bin",
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task GIVEN_PreManifestOrphanWithDurableOwner_WHEN_FreshRecoveryRuns_THEN_ShouldCleanItUnderSolutionLock()
    {
        var transaction = await CreateTransactionAsync(RecoveryState.Prepared);
        File.Delete(Path.Combine(_stateDirectory, "recovery", transaction.Manifest.CommitId, "manifest.json"));

        await CreateFreshRecoveryService().RecoverAsync(TestContext.Current.CancellationToken);

        Directory.EnumerateFileSystemEntries(Path.Combine(_stateDirectory, "recovery")).Should().BeEmpty();
        (await File.ReadAllBytesAsync(transaction.ReplacePath, TestContext.Current.CancellationToken)).Should().Equal(transaction.ReplaceOriginal);
    }

    [Fact]
    public async Task GIVEN_MalformedManifest_WHEN_ReadingRecoveryStatus_THEN_ShouldExposeGlobalRecoveryConflict()
    {
        var directory = Path.Combine(_stateDirectory, "recovery", "malformed");
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
        }
        else
        {
            Directory.CreateDirectory(
                directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var manifestPath = Path.Combine(directory, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, "{", TestContext.Current.CancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                manifestPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        var statuses = await _store.GetStatusesAsync(TestContext.Current.CancellationToken);

        statuses.Should().ContainSingle();
        statuses[0].State.Should().Be(RecoveryState.RecoveryConflict);
        statuses[0].SolutionPath.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_ManifestWithEscapingArtifact_WHEN_ReadingRecoveryStatus_THEN_ShouldExposeRecoveryConflictWithoutUsingArtifact()
    {
        var transaction = await CreateTransactionAsync(RecoveryState.Applying);
        var unsafeManifest = transaction.Manifest with
        {
            Entries =
            [
                transaction.Manifest.Entries[0] with { BackupPath = $"..{Path.DirectorySeparatorChar}outside.bin" },
            ],
        };

        await _store.WriteManifestAsync(unsafeManifest, TestContext.Current.CancellationToken);

        var statuses = await _store.GetStatusesAsync(TestContext.Current.CancellationToken);

        statuses.Should().ContainSingle();
        statuses[0].State.Should().Be(RecoveryState.RecoveryConflict);
        statuses[0].SolutionPath.Should().Be(transaction.Manifest.LoadedPath);
    }

    [Fact]
    public async Task GIVEN_LiveCommitOwnerProcess_WHEN_AcquiringSameWorkspaceRoot_THEN_ShouldBlockUntilOwnerReleases()
    {
        var manager = CreateLockManager(_fileSystem);
        manager.Acquire(_root).Lock!.Dispose();
        var lockPath = GetLockPath();
        using var process = await StartLockOwnerAsync(lockPath);
        try
        {
            var contended = manager.Acquire(_root);

            contended.Status.Should().Be(
                WorkspaceCommitLockAcquisitionStatus.Contended,
                contended.ErrorMessage);

            await process.StandardInput.WriteLineAsync();
            await process.StandardInput.FlushAsync(TestContext.Current.CancellationToken);
            await WaitForExitAsync(process, TestContext.Current.CancellationToken);
            process.ExitCode.Should().Be(0);
            using var reacquired = manager.Acquire(_root).Lock;
            reacquired.Should().NotBeNull();
        }
        finally
        {
            await EnsureProcessExitAsync(process);
        }
    }

    [Fact]
    public void GIVEN_DifferentWorkspaceRoots_WHEN_AcquiringCommitLocks_THEN_ShouldNotContend()
    {
        var manager = CreateLockManager(_fileSystem);
        var firstRoot = Path.Combine(_root, "first");
        var secondRoot = Path.Combine(_root, "second");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        using var first = manager.Acquire(firstRoot).Lock;
        using var second = manager.Acquire(secondRoot).Lock;

        first.Should().NotBeNull();
        second.Should().NotBeNull();
    }

    [Fact]
    public async Task GIVEN_ExternalOwnerProcessTerminates_WHEN_AcquiringCommitLock_THEN_ShouldRecoverCrashReleasedOwnership()
    {
        var manager = CreateLockManager(_fileSystem);
        manager.Acquire(_root).Lock!.Dispose();
        using var process = await StartLockOwnerAsync(GetLockPath());
        try
        {
            manager.Acquire(_root).Status.Should().Be(WorkspaceCommitLockAcquisitionStatus.Contended);
            process.Kill(entireProcessTree: true);
            await WaitForExitAsync(process, TestContext.Current.CancellationToken);

            using var recovered = manager.Acquire(_root).Lock;
            recovered.Should().NotBeNull();
        }
        finally
        {
            await EnsureProcessExitAsync(process);
        }
    }

    public void Dispose()
    {
        TemporaryDirectory.Attach(_root).Dispose();
    }

    private WorkspaceCommitRecoveryService CreateFreshRecoveryService()
    {
        var fileSystem = new FileSystem();
        var fileCommitter = new NativeAtomicFileCommitter();
        var atomicWriter = new AtomicFileWriter(fileSystem, fileCommitter);
        var pathContainment = CreatePathContainment(fileSystem);
        var stateDirectorySecurity = new WorkspaceStateDirectorySecurity(fileSystem);
        var stateDirectory = new WorkspaceStateDirectory(
            Options.Create(new WorkspaceOptions { StateDirectory = _stateDirectory }),
            fileSystem,
            stateDirectorySecurity);

        stateDirectory.Initialize();
        var store = new CommitRecoveryStore(
            fileSystem,
            atomicWriter,
            new WorkspacePathComparison(),
            pathContainment,
            stateDirectory,
            stateDirectorySecurity);

        var writer = new WorkspaceCommitWriter(
            fileSystem,
            atomicWriter,
            store,
            fileCommitter,
            pathContainment);

        return new WorkspaceCommitRecoveryService(store, writer, CreateLockManager(fileSystem));
    }

    private static WorkspaceCommitLockManager CreateLockManager(IFileSystem fileSystem)
    {
        var pathContainment = CreatePathContainment(fileSystem);
        return new WorkspaceCommitLockManager(
            fileSystem,
            new FileStreamWorkspaceFileLockProvider(),
            pathContainment);
    }

    private static PhysicalPathContainment CreatePathContainment(IFileSystem fileSystem)
    {
        var pathComparison = new WorkspacePathComparison();
        return new PhysicalPathContainment(fileSystem, pathComparison);
    }

    private string GetLockPath()
    {
        return Path.Combine(_root, ".vs", "roslyn-workbench-mcp", "locks", "commit.lock");
    }

    private static async Task<Process> StartLockOwnerAsync(string lockPath)
    {
        var executableName = OperatingSystem.IsWindows()
            ? "Roslyn.Workbench.Mcp.Workspace.LockFixture.exe"
            : "Roslyn.Workbench.Mcp.Workspace.LockFixture";

        var startInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, executableName))
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add(lockPath);
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the Workspace lock fixture process.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(_processTimeout);
            var signal = await process.StandardOutput.ReadLineAsync(timeout.Token);
            signal.Should().Be("LOCKED");
            return process;
        }
        catch
        {
            await EnsureProcessExitAsync(process);
            process.Dispose();
            throw;
        }
    }

    private static async Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_processTimeout);
        await process.WaitForExitAsync(timeout.Token);
    }

    private static async Task EnsureProcessExitAsync(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        // Teardown uses its own timeout so a cancelled test cannot leave the lock-owner process alive.
        process.StandardInput.Close();
        using var gracefulTimeout = new CancellationTokenSource(_processTimeout);
        try
        {
            await process.WaitForExitAsync(gracefulTimeout.Token);
            return;
        }
        catch (OperationCanceledException)
        {
        }

        process.Kill(entireProcessTree: true);
        using var forcedTimeout = new CancellationTokenSource(_processTimeout);
        await process.WaitForExitAsync(forcedTimeout.Token);
    }

    private async Task<TransactionFixture> CreateTransactionAsync(RecoveryState state)
    {
        var sourceDirectory = Path.Combine(_root, "source");
        var createdDirectory = Path.Combine(sourceDirectory, "generated");
        Directory.CreateDirectory(sourceDirectory);
        var replacePath = Path.Combine(sourceDirectory, "Replace.cs");
        var createPath = Path.Combine(createdDirectory, "Create.cs");
        var deletePath = Path.Combine(sourceDirectory, "Delete.cs");
        var deleteMarkerPath = $"{deletePath}.commit.delete";
        var replaceOriginal = new byte[] { 0xFF, 0xFE, 65, 0, 0, 1 };
        var replaceIntended = new byte[] { 0xEF, 0xBB, 0xBF, 66, 10, 0 };
        var createIntended = new byte[] { 0, 10, 20, 30, 255 };
        var deleteOriginal = new byte[] { 7, 6, 5, 0, 4 };
        await File.WriteAllBytesAsync(replacePath, replaceOriginal, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(deletePath, deleteOriginal, TestContext.Current.CancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(replacePath, _replaceUnixFileMode);
        }

        var manifest = new WorkspaceCommitManifest
        {
            CommitId = "commit",
            LoadedPath = Path.Combine(_root, "Sample.slnx"),
            WorkspaceRoot = _root,
            State = state,
            CreatedDirectories = [createdDirectory],
            Entries =
            [
                new WorkspaceCommitEntry
                {
                    TargetPath = replacePath,
                    Operation = WorkspaceFileOperation.Replace,
                    OriginalExists = true,
                    OriginalHash = Hash(replaceOriginal),
                    IntendedHash = Hash(replaceIntended),
                    OriginalUnixFileMode = OperatingSystem.IsWindows()
                        ? null
                        : _replaceUnixFileMode,
                    BackupPath = "backup/replace.bin",
                    StagedPath = "staged/replace.bin",
                },
                new WorkspaceCommitEntry
                {
                    TargetPath = createPath,
                    Operation = WorkspaceFileOperation.Create,
                    OriginalExists = false,
                    IntendedHash = Hash(createIntended),
                    StagedPath = "staged/create.bin",
                },
                new WorkspaceCommitEntry
                {
                    TargetPath = deletePath,
                    Operation = WorkspaceFileOperation.Delete,
                    OriginalExists = true,
                    OriginalHash = Hash(deleteOriginal),
                    BackupPath = "backup/delete.bin",
                    DeleteMarkerPath = deleteMarkerPath,
                },
            ],
        };

        await _store.PersistPlanAsync(new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["backup/replace.bin"] = replaceOriginal,
            ["staged/replace.bin"] = replaceIntended,
            ["staged/create.bin"] = createIntended,
            ["backup/delete.bin"] = deleteOriginal,
        }), TestContext.Current.CancellationToken);

        if (state != RecoveryState.Prepared)
        {
            await _store.WriteManifestAsync(manifest, TestContext.Current.CancellationToken);
        }

        return new TransactionFixture(
            manifest,
            replacePath,
            replaceOriginal,
            replaceIntended,
            createPath,
            createIntended,
            deletePath,
            deleteOriginal,
            deleteMarkerPath,
            createdDirectory);
    }

    private static string Hash(byte[] contents)
    {
        return Convert.ToHexString(SHA256.HashData(contents));
    }

    private sealed class TransactionFixture
    {
        public WorkspaceCommitManifest Manifest { get; }

        public string ReplacePath { get; }

        public byte[] ReplaceOriginal { get; }

        public byte[] ReplaceIntended { get; }

        public string CreatePath { get; }

        public byte[] CreateIntended { get; }

        public string DeletePath { get; }

        public byte[] DeleteOriginal { get; }

        public string DeleteMarkerPath { get; }

        public string CreatedDirectory { get; }

        public TransactionFixture(
            WorkspaceCommitManifest manifest,
            string replacePath,
            byte[] replaceOriginal,
            byte[] replaceIntended,
            string createPath,
            byte[] createIntended,
            string deletePath,
            byte[] deleteOriginal,
            string deleteMarkerPath,
            string createdDirectory)
        {
            Manifest = manifest;
            ReplacePath = replacePath;
            ReplaceOriginal = replaceOriginal;
            ReplaceIntended = replaceIntended;
            CreatePath = createPath;
            CreateIntended = createIntended;
            DeletePath = deletePath;
            DeleteOriginal = deleteOriginal;
            DeleteMarkerPath = deleteMarkerPath;
            CreatedDirectory = createdDirectory;
        }
    }
}
