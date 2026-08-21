using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceExternalInputChangeMonitorTests : IDisposable
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IFileSystemWatcherFactory> _watcherFactory;
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly List<WorkspaceExternalInputChangeMonitor> _targets;

    public WorkspaceExternalInputChangeMonitorTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _directory = new Mock<IDirectory>();
        _watcherFactory = new Mock<IFileSystemWatcherFactory>();
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _targets = [];
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.FileSystemWatcher).Returns(_watcherFactory.Object);
        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: true));
    }

    [Fact]
    public void GIVEN_ExternalRoot_WHEN_Started_THEN_ShouldConfigureAndEnableRecursiveWatcherOnce()
    {
        var externalRoot = CreateExternalRoot();
        var watcher = ConfigureExistingRoot(externalRoot);
        var membership = CreateMembership(externalRoot);
        var target = CreateTarget(membership);

        target.Start();
        target.Start();

        watcher.VerifySet(item => item.IncludeSubdirectories = true, Times.Once);
        watcher.VerifySet(item => item.InternalBufferSize = 64 * 1024, Times.Once);
        watcher.VerifySet(item => item.NotifyFilter = NotifyFilters.DirectoryName
            | NotifyFilters.FileName
            | NotifyFilters.LastWrite
            | NotifyFilters.Size, Times.Once);

        watcher.VerifySet(item => item.EnableRaisingEvents = true, Times.Once);
    }

    [Fact]
    public void GIVEN_NoExternalMemberships_WHEN_Checking_THEN_ShouldRemainUnchanged()
    {
        var target = CreateTarget();

        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);

        target.Change.Should().BeNull();
        _watcherFactory.Verify(item => item.New(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_MatchingCreatedFile_WHEN_WatcherRaisesEvent_THEN_ShouldReportChange()
    {
        var externalRoot = CreateExternalRoot();
        var createdPath = Path.Combine(externalRoot, "Created.cs");
        var watcher = ConfigureExistingRoot(externalRoot);
        var membership = CreateMembership(
            externalRoot,
            matches: static path => Path.GetExtension(path) == ".cs");

        var target = CreateTarget(membership);
        target.Start();
        CompleteInitialCheck(target, externalRoot);

        watcher.Raise(
            item => item.Created += null,
            new FileSystemEventArgs(WatcherChangeTypes.Created, externalRoot, "Created.cs"));

        WaitForChange(target);
        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Created,
            Path = createdPath,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_NonMatchingCreatedFile_WHEN_WatcherRaisesEvent_THEN_ShouldIgnoreChange()
    {
        var externalRoot = CreateExternalRoot();
        var watcher = ConfigureExistingRoot(externalRoot);
        var membership = CreateMembership(
            externalRoot,
            matches: static path => Path.GetExtension(path) == ".cs");

        var target = CreateTarget(membership);
        target.Start();
        CompleteInitialCheck(target, externalRoot);

        watcher.Raise(
            item => item.Created += null,
            new FileSystemEventArgs(WatcherChangeTypes.Created, externalRoot, "Created.txt"));

        target.WaitForPendingEvents(CancellationToken.None);
        target.Change.Should().BeNull();
    }

    [Fact]
    public void GIVEN_PopulatedDirectoryIsCreated_WHEN_WatcherRaisesDirectoryEvent_THEN_ShouldReportCreatedMember()
    {
        var externalRoot = CreateExternalRoot();
        var insertedDirectory = Path.Combine(externalRoot, "Inserted");
        var insertedPath = Path.Combine(insertedDirectory, "Inserted.cs");
        var watcher = ConfigureExistingRoot(externalRoot);
        _directory.Setup(item => item.Exists(insertedDirectory)).Returns(true);
        _directory
            .SetupSequence(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([])
            .Returns([insertedPath]);

        var membership = CreateMembership(
            externalRoot,
            matches: static path => Path.GetExtension(path) == ".cs");

        var target = CreateTarget(membership);
        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);

        watcher.Raise(
            item => item.Created += null,
            new FileSystemEventArgs(WatcherChangeTypes.Created, externalRoot, "Inserted"));

        WaitForChange(target);
        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            Kind = WorkspaceInputChangeKind.Created,
            Path = insertedPath,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_PopulatedDirectoryIsDeleted_WHEN_WatcherRaisesDirectoryEvent_THEN_ShouldReportDeletedMember()
    {
        var externalRoot = CreateExternalRoot();
        var deletedDirectory = Path.Combine(externalRoot, "Deleted");
        var deletedPath = Path.Combine(deletedDirectory, "Deleted.cs");
        var watcher = ConfigureExistingRoot(externalRoot);
        _directory
            .SetupSequence(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([deletedPath])
            .Returns([]);

        var membership = CreateMembership(
            externalRoot,
            matches: static path => Path.GetExtension(path) == ".cs",
            loadedPaths: [deletedPath]);

        var target = CreateTarget(membership);
        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);

        watcher.Raise(
            item => item.Deleted += null,
            new FileSystemEventArgs(WatcherChangeTypes.Deleted, externalRoot, "Deleted"));

        WaitForChange(target);
        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            Kind = WorkspaceInputChangeKind.Deleted,
            Path = deletedPath,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_PopulatedDirectoryIsRenamedOutsideRoot_WHEN_WatcherRaisesDirectoryEvent_THEN_ShouldReportDeletedMember()
    {
        var externalRoot = CreateExternalRoot();
        var previousDirectory = Path.Combine(externalRoot, "Previous");
        var previousPath = Path.Combine(previousDirectory, "Previous.cs");
        var watcher = ConfigureExistingRoot(externalRoot);
        _directory
            .SetupSequence(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([previousPath])
            .Returns([]);

        var membership = CreateMembership(
            externalRoot,
            matches: static path => Path.GetExtension(path) == ".cs",
            loadedPaths: [previousPath]);

        var target = CreateTarget(membership);
        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);

        watcher.Raise(
            item => item.Renamed += null,
            new RenamedEventArgs(WatcherChangeTypes.Renamed, externalRoot, "Outside", "Previous"));

        WaitForChange(target);
        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            Kind = WorkspaceInputChangeKind.Deleted,
            Path = previousPath,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_MembershipChangedBeforeWatcherStarts_WHEN_Checking_THEN_ShouldReportCreatedPath()
    {
        var externalRoot = CreateExternalRoot();
        var loadedPath = Path.Combine(externalRoot, "Loaded.cs");
        var createdPath = Path.Combine(externalRoot, "Created.cs");
        ConfigureExistingRoot(externalRoot);
        _directory
            .Setup(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([loadedPath, createdPath]);

        var membership = CreateMembership(externalRoot, loadedPaths: [loadedPath]);
        var target = CreateTarget(membership);
        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);

        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            Kind = WorkspaceInputChangeKind.Created,
            Path = createdPath,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_MultipleCreatedMembers_WHEN_Checking_THEN_ShouldReportOrdinallyFirstPath()
    {
        var externalRoot = CreateExternalRoot();
        var laterPath = Path.Combine(externalRoot, "Z.cs");
        var earlierPath = Path.Combine(externalRoot, "A.cs");
        ConfigureExistingRoot(externalRoot);
        _directory
            .Setup(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([laterPath, earlierPath]);

        var membership = CreateMembership(externalRoot);
        var target = CreateTarget(membership);

        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);

        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            Kind = WorkspaceInputChangeKind.Created,
            Path = earlierPath,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_MissingRootWithLoadedFile_WHEN_Checking_THEN_ShouldReportDeletedPath()
    {
        var externalRoot = CreateExternalRoot();
        var loadedPath = Path.Combine(externalRoot, "Loaded.cs");
        _directory.Setup(item => item.Exists(externalRoot)).Returns(false);
        var membership = CreateMembership(externalRoot, loadedPaths: [loadedPath]);
        var target = CreateTarget(membership);

        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);

        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            Kind = WorkspaceInputChangeKind.Deleted,
            Path = loadedPath,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_InitiallyMissingRoot_WHEN_MatchingFileAppears_THEN_ShouldStartWatcherAndReportChange()
    {
        var externalRoot = CreateExternalRoot();
        var createdPath = Path.Combine(externalRoot, "Created.cs");
        var watcher = new Mock<IFileSystemWatcher>();
        _directory.SetupSequence(item => item.Exists(externalRoot))
            .Returns(false)
            .Returns(false)
            .Returns(true);

        _directory
            .Setup(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([createdPath]);

        _watcherFactory.Setup(item => item.New(externalRoot)).Returns(watcher.Object);
        var membership = CreateMembership(externalRoot);
        var target = CreateTarget(membership);

        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);
        target.Change.Should().BeNull();
        target.WaitForPendingEvents(CancellationToken.None);

        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            Kind = WorkspaceInputChangeKind.Created,
            Path = createdPath,
        };

        target.Change.Should().BeEquivalentTo(expected);
        watcher.VerifySet(item => item.EnableRaisingEvents = true, Times.Once);
    }

    [Fact]
    public void GIVEN_WatchedRootIsRecreated_WHEN_MatchingFileAppears_THEN_ShouldReplaceWatcherAndReportChange()
    {
        var externalRoot = CreateExternalRoot();
        var createdPath = Path.Combine(externalRoot, "Created.cs");
        var initialWatcher = new Mock<IFileSystemWatcher>();
        var replacementWatcher = new Mock<IFileSystemWatcher>();
        _directory.SetupSequence(item => item.Exists(externalRoot))
            .Returns(true)
            .Returns(true)
            .Returns(false)
            .Returns(true);

        _directory
            .SetupSequence(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([])
            .Returns([createdPath]);

        _watcherFactory.SetupSequence(item => item.New(externalRoot))
            .Returns(initialWatcher.Object)
            .Returns(replacementWatcher.Object);

        var membership = CreateMembership(externalRoot);
        var target = CreateTarget(membership);
        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);
        target.WaitForPendingEvents(CancellationToken.None);
        target.WaitForPendingEvents(CancellationToken.None);

        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            Kind = WorkspaceInputChangeKind.Created,
            Path = createdPath,
        };

        target.Change.Should().BeEquivalentTo(expected);
        initialWatcher.VerifySet(item => item.EnableRaisingEvents = false, Times.Once);
        initialWatcher.Verify(item => item.Dispose(), Times.Once);
        replacementWatcher.VerifySet(item => item.EnableRaisingEvents = true, Times.Once);
    }

    [Fact]
    public void GIVEN_WatcherCannotStart_WHEN_CheckingUnchangedMembership_THEN_ShouldPollEveryTime()
    {
        var externalRoot = CreateExternalRoot();
        var loadedPath = Path.Combine(externalRoot, "Loaded.cs");
        _directory.Setup(item => item.Exists(externalRoot)).Returns(true);
        _directory
            .Setup(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([loadedPath]);

        _watcherFactory.Setup(item => item.New(externalRoot)).Throws(new IOException("Watcher unavailable."));
        var membership = CreateMembership(externalRoot, loadedPaths: [loadedPath]);
        var target = CreateTarget(membership);

        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);
        target.WaitForPendingEvents(CancellationToken.None);

        target.Change.Should().BeNull();
        _directory.Verify(
            item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories),
            Times.Exactly(2));
    }

    [Fact]
    public void GIVEN_WatcherConfigurationFails_WHEN_Starting_THEN_ShouldDisposeWatcherAndPoll()
    {
        var externalRoot = CreateExternalRoot();
        var watcher = new Mock<IFileSystemWatcher>();
        watcher
            .SetupSet(item => item.IncludeSubdirectories = true)
            .Throws(new IOException("Watcher configuration failed."));

        _directory.Setup(item => item.Exists(externalRoot)).Returns(true);
        _directory
            .Setup(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([]);

        _watcherFactory.Setup(item => item.New(externalRoot)).Returns(watcher.Object);
        var membership = CreateMembership(externalRoot);
        var target = CreateTarget(membership);

        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);

        target.Change.Should().BeNull();
        watcher.Verify(item => item.Dispose(), Times.Exactly(2));
    }

    [Fact]
    public void GIVEN_WatcherCannotBeEnabled_WHEN_Starting_THEN_ShouldDisposeWatcherAndPoll()
    {
        var externalRoot = CreateExternalRoot();
        var watcher = new Mock<IFileSystemWatcher>();
        watcher
            .SetupSet(item => item.EnableRaisingEvents = true)
            .Throws(new PlatformNotSupportedException("Watcher cannot be enabled."));

        _directory.Setup(item => item.Exists(externalRoot)).Returns(true);
        _directory
            .Setup(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([]);

        _watcherFactory.Setup(item => item.New(externalRoot)).Returns(watcher.Object);
        var membership = CreateMembership(externalRoot);
        var target = CreateTarget(membership);

        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);

        target.Change.Should().BeNull();
        watcher.Verify(item => item.Dispose(), Times.Exactly(2));
    }

    [Fact]
    public void GIVEN_DuplicateRoots_WHEN_Started_THEN_ShouldCreateOneWatcher()
    {
        var externalRoot = CreateExternalRoot();
        ConfigureExistingRoot(externalRoot);
        var firstMembership = CreateMembership(externalRoot);
        var secondMembership = CreateMembership(externalRoot);
        var target = CreateTarget(firstMembership, secondMembership);

        target.Start();

        _watcherFactory.Verify(item => item.New(externalRoot), Times.Once);
    }

    [Fact]
    public void GIVEN_MembershipCannotBeEnumerated_WHEN_Checking_THEN_ShouldReportFailure()
    {
        var externalRoot = CreateExternalRoot();
        ConfigureExistingRoot(externalRoot);
        _directory
            .Setup(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Throws(new UnauthorizedAccessException());
        var membership = CreateMembership(externalRoot);
        var target = CreateTarget(membership);

        target.Start();
        target.WaitForPendingEvents(CancellationToken.None);

        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            ErrorCode = WorkspaceInputChangeErrorCode.MembershipEnumerationFailure,
            Kind = WorkspaceInputChangeKind.MembershipError,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_CancelledInitialCheck_WHEN_Retrying_THEN_ShouldPerformCompleteCheck()
    {
        var externalRoot = CreateExternalRoot();
        var loadedPath = Path.Combine(externalRoot, "Loaded.cs");
        ConfigureExistingRoot(externalRoot);
        _directory
            .Setup(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([loadedPath]);

        var membership = CreateMembership(externalRoot, loadedPaths: [loadedPath]);
        var target = CreateTarget(membership);
        target.Start();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = () => target.WaitForPendingEvents(cancellationSource.Token);

        action.Should().Throw<OperationCanceledException>();
        target.WaitForPendingEvents(CancellationToken.None);
        target.Change.Should().BeNull();
        _directory.Verify(
            item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories),
            Times.Once);
    }

    [Theory]
    [InlineData(WatcherChangeTypes.Changed, (int)WorkspaceInputChangeKind.Changed)]
    [InlineData(WatcherChangeTypes.Deleted, (int)WorkspaceInputChangeKind.Deleted)]
    public void GIVEN_MatchingFile_WHEN_WatcherRaisesEvent_THEN_ShouldPreserveChangeKind(
        WatcherChangeTypes watcherChangeType,
        int expectedKindValue)
    {
        var externalRoot = CreateExternalRoot();
        var path = Path.Combine(externalRoot, "Document.cs");
        var watcher = ConfigureExistingRoot(externalRoot);
        var membership = CreateMembership(externalRoot);
        var target = CreateTarget(membership);
        target.Start();
        CompleteInitialCheck(target, externalRoot);
        var args = new FileSystemEventArgs(watcherChangeType, externalRoot, "Document.cs");

        if (watcherChangeType == WatcherChangeTypes.Changed)
        {
            watcher.Raise(item => item.Changed += null, args);
        }
        else
        {
            watcher.Raise(item => item.Deleted += null, args);
        }

        WaitForChange(target);
        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = (WorkspaceInputChangeKind)expectedKindValue,
            Path = path,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_PreviousMatchingPath_WHEN_FileIsRenamedOutsideGlob_THEN_ShouldReportRename()
    {
        var externalRoot = CreateExternalRoot();
        var previousPath = Path.Combine(externalRoot, "Before.cs");
        var path = Path.Combine(externalRoot, "After.txt");
        var watcher = ConfigureExistingRoot(externalRoot);
        var membership = CreateMembership(
            externalRoot,
            matches: static candidate => Path.GetExtension(candidate) == ".cs");

        var target = CreateTarget(membership);
        target.Start();
        CompleteInitialCheck(target, externalRoot);

        watcher.Raise(
            item => item.Renamed += null,
            new RenamedEventArgs(WatcherChangeTypes.Renamed, externalRoot, "After.txt", "Before.cs"));

        WaitForChange(target);
        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Renamed,
            Path = path,
            PreviousPath = previousPath,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(true, (int)WorkspaceInputChangeErrorCode.WatcherBufferOverflow)]
    [InlineData(false, (int)WorkspaceInputChangeErrorCode.WatcherFailure)]
    public void GIVEN_WatcherError_WHEN_Raised_THEN_ShouldReportExpectedFailure(
        bool isBufferOverflow,
        int expectedErrorValue)
    {
        var externalRoot = CreateExternalRoot();
        var watcher = ConfigureExistingRoot(externalRoot);
        var membership = CreateMembership(externalRoot);
        var target = CreateTarget(membership);
        target.Start();
        Exception exception = isBufferOverflow
            ? new InternalBufferOverflowException()
            : new IOException();

        watcher.Raise(item => item.Error += null, new ErrorEventArgs(exception));

        WaitForChange(target);
        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            ErrorCode = (WorkspaceInputChangeErrorCode)expectedErrorValue,
            Kind = WorkspaceInputChangeKind.WatcherError,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_MultipleMatchingEvents_WHEN_Raised_THEN_ShouldRetainFirstChange()
    {
        var externalRoot = CreateExternalRoot();
        var watcher = ConfigureExistingRoot(externalRoot);
        using var firstMatchStarted = new ManualResetEventSlim();
        using var continueFirstMatch = new ManualResetEventSlim();
        var membership = CreateMembership(
            externalRoot,
            path => MatchAfterSecondEventIsQueued(
                path,
                firstMatchStarted,
                continueFirstMatch));

        var target = CreateTarget(membership);
        target.Start();
        CompleteInitialCheck(target, externalRoot);
        watcher.Raise(
            item => item.Created += null,
            new FileSystemEventArgs(WatcherChangeTypes.Created, externalRoot, "First.cs"));
        firstMatchStarted.Wait(TestContext.Current.CancellationToken);
        watcher.Raise(
            item => item.Created += null,
            new FileSystemEventArgs(WatcherChangeTypes.Created, externalRoot, "Second.cs"));
        continueFirstMatch.Set();

        WaitForChange(target);

        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Created,
            Path = Path.Combine(externalRoot, "First.cs"),
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_ChangeAlreadyRecorded_WHEN_AnotherEventIsRaised_THEN_ShouldRetainFirstChange()
    {
        var externalRoot = CreateExternalRoot();
        var watcher = ConfigureExistingRoot(externalRoot);
        var membership = CreateMembership(externalRoot);
        var target = CreateTarget(membership);
        target.Start();
        CompleteInitialCheck(target, externalRoot);
        watcher.Raise(
            item => item.Created += null,
            new FileSystemEventArgs(WatcherChangeTypes.Created, externalRoot, "First.cs"));
        WaitForChange(target);

        watcher.Raise(
            item => item.Created += null,
            new FileSystemEventArgs(WatcherChangeTypes.Created, externalRoot, "Second.cs"));

        target.WaitForPendingEvents(CancellationToken.None);
        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Created,
            Path = Path.Combine(externalRoot, "First.cs"),
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_WatcherChangeDuringMembershipCheck_WHEN_BothDetectChanges_THEN_ShouldRetainFirstChange()
    {
        var externalRoot = CreateExternalRoot();
        var polledPath = Path.Combine(externalRoot, "Polled.cs");
        var watcherPath = Path.Combine(externalRoot, "Watcher.cs");
        var watcher = ConfigureExistingRoot(externalRoot);
        using var pollingMatchStarted = new ManualResetEventSlim();
        using var continuePollingMatch = new ManualResetEventSlim();
        var membership = CreateMembership(
            externalRoot,
            path => MatchAfterWatcherChange(
                path,
                polledPath,
                pollingMatchStarted,
                continuePollingMatch));

        _directory
            .Setup(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([polledPath]);

        var target = CreateTarget(membership);
        target.Start();
        var checkTask = Task.Run(
            () => target.WaitForPendingEvents(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        pollingMatchStarted.Wait(TestContext.Current.CancellationToken);

        watcher.Raise(
            item => item.Created += null,
            new FileSystemEventArgs(WatcherChangeTypes.Created, externalRoot, "Watcher.cs"));

        var spinWait = new SpinWait();
        while (target.Change is null)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            spinWait.SpinOnce();
        }

        continuePollingMatch.Set();
        await checkTask;

        var expected = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Created,
            Path = watcherPath,
        };

        target.Change.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GIVEN_Monitor_WHEN_DisposedTwice_THEN_ShouldDisposeWatcherOnce()
    {
        var externalRoot = CreateExternalRoot();
        var watcher = ConfigureExistingRoot(externalRoot);
        var membership = CreateMembership(externalRoot);
        var target = CreateTarget(membership);
        target.Start();

        target.Dispose();
        target.Dispose();

        watcher.Verify(item => item.Dispose(), Times.Once);
    }

    public void Dispose()
    {
        foreach (var target in _targets)
        {
            target.Dispose();
        }
    }

    private WorkspaceExternalInputChangeMonitor CreateTarget(params WorkspaceExternalInputMembership[] memberships)
    {
        var target = new WorkspaceExternalInputChangeMonitor(
            _fileSystem.Object,
            _pathComparison.Object,
            memberships);

        _targets.Add(target);
        return target;
    }

    private Mock<IFileSystemWatcher> ConfigureExistingRoot(string externalRoot)
    {
        var watcher = new Mock<IFileSystemWatcher>();
        _directory.Setup(item => item.Exists(externalRoot)).Returns(true);
        _watcherFactory.Setup(item => item.New(externalRoot)).Returns(watcher.Object);
        return watcher;
    }

    private WorkspaceExternalInputMembership CreateMembership(
        string externalRoot,
        Func<string, bool>? matches = null,
        IReadOnlyList<string>? loadedPaths = null)
    {
        var matcher = new Mock<IWorkspaceItemGlobMatcher>();
        matcher
            .Setup(item => item.Matches(It.IsAny<string>()))
            .Returns((string path) => matches?.Invoke(path) ?? true);

        var glob = new WorkspaceEvaluatedItemGlob(matcher.Object, [externalRoot]);
        var loadedPathKeys = (loadedPaths ?? [])
            .Select(path => _pathComparison.Object.CreateKey(path))
            .ToHashSet();
        var rootKey = _pathComparison.Object.CreateKey(externalRoot);
        var membership = new WorkspaceExternalInputMembership(
            rootKey,
            [glob],
            loadedPathKeys);

        return membership;
    }

    private void CompleteInitialCheck(WorkspaceExternalInputChangeMonitor target, string externalRoot)
    {
        _directory
            .Setup(item => item.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories))
            .Returns([]);

        target.WaitForPendingEvents(CancellationToken.None);
    }

    private static void WaitForChange(WorkspaceExternalInputChangeMonitor target)
    {
        target.WaitForPendingEvents(CancellationToken.None);
        target.Change.Should().NotBeNull();
    }

    private static string CreateExternalRoot()
    {
        return Path.Combine(Path.GetTempPath(), "External");
    }

    private static bool MatchAfterSecondEventIsQueued(
        string path,
        ManualResetEventSlim firstMatchStarted,
        ManualResetEventSlim continueFirstMatch)
    {
        if (string.Equals(Path.GetFileName(path), "First.cs", StringComparison.Ordinal))
        {
            firstMatchStarted.Set();
            continueFirstMatch.Wait(TestContext.Current.CancellationToken);
        }

        return true;
    }

    private static bool MatchAfterWatcherChange(
        string path,
        string polledPath,
        ManualResetEventSlim pollingMatchStarted,
        ManualResetEventSlim continuePollingMatch)
    {
        if (string.Equals(path, polledPath, StringComparison.Ordinal))
        {
            pollingMatchStarted.Set();
            continuePollingMatch.Wait(TestContext.Current.CancellationToken);
        }

        return true;
    }
}
