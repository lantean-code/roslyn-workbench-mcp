using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceInputChangeMonitorTests : IDisposable
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IFileSystemWatcherFactory> _watcherFactory;
    private readonly Mock<IFileSystemWatcher> _watcher;
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly string _workspaceRoot;
    private readonly WorkspaceInputChangeMonitor _target;

    public WorkspaceInputChangeMonitorTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _watcherFactory = new Mock<IFileSystemWatcherFactory>();
        _watcher = new Mock<IFileSystemWatcher>();
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "Workspace");
        _fileSystem.SetupGet(item => item.FileSystemWatcher).Returns(_watcherFactory.Object);
        _watcherFactory.Setup(item => item.New(_workspaceRoot)).Returns(_watcher.Object);
        _pathComparison
            .Setup(item => item.GetComparer(_workspaceRoot))
            .Returns(StringComparer.Ordinal);

        _target = new WorkspaceInputChangeMonitor(
            _fileSystem.Object,
            _pathComparison.Object,
            _workspaceRoot);
    }

    [Fact]
    public void GIVEN_NewMonitor_WHEN_Constructed_THEN_ShouldConfigureRecursiveWatchingWithoutEnablingIt()
    {
        _watcher.VerifySet(item => item.IncludeSubdirectories = true, Times.Once);
        _watcher.VerifySet(item => item.InternalBufferSize = 64 * 1024, Times.Once);
        _watcher.VerifySet(item => item.NotifyFilter = NotifyFilters.DirectoryName
            | NotifyFilters.FileName
            | NotifyFilters.LastWrite
            | NotifyFilters.Size, Times.Once);

        _watcher.VerifySet(item => item.EnableRaisingEvents = true, Times.Never);
        _target.Change.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NewMonitor_WHEN_StartedMultipleTimes_THEN_ShouldEnableWatcherOnce()
    {
        _target.Start();
        _target.Start();

        _watcher.VerifySet(item => item.EnableRaisingEvents = true, Times.Once);
    }

    [Fact]
    public void GIVEN_StartedMonitorWithBufferedChange_WHEN_InputsAreTracked_THEN_ShouldReportRelevantChange()
    {
        _target.Start();
        var args = new FileSystemEventArgs(WatcherChangeTypes.Changed, _workspaceRoot, "Document.cs");
        _watcher.Raise(item => item.Changed += null, args);

        TrackWorkspaceInputs();

        WaitForChange();
        _target.Change.Should().BeEquivalentTo(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Changed,
            Path = Path.Combine(_workspaceRoot, "Document.cs"),
        });
    }

    [Fact]
    public void GIVEN_StartedMonitorWithBufferedRename_WHEN_InputsAreTracked_THEN_ShouldReportRelevantChange()
    {
        _target.Start();
        var args = new RenamedEventArgs(
            WatcherChangeTypes.Renamed,
            _workspaceRoot,
            "Renamed.cs",
            "Document.cs");

        _watcher.Raise(item => item.Renamed += null, args);
        TrackWorkspaceInputs();

        WaitForChange();
        _target.Change.Should().BeEquivalentTo(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Renamed,
            Path = Path.Combine(_workspaceRoot, "Renamed.cs"),
            PreviousPath = Path.Combine(_workspaceRoot, "Document.cs"),
        });
    }

    [Fact]
    public void GIVEN_StartedMonitorWithBufferedIrrelevantChange_WHEN_InputsAreTracked_THEN_ShouldIgnoreChange()
    {
        _target.Start();
        var args = new FileSystemEventArgs(WatcherChangeTypes.Changed, _workspaceRoot, "Untracked.cs");
        _watcher.Raise(item => item.Changed += null, args);

        TrackWorkspaceInputs();

        _target.WaitForPendingEvents(CancellationToken.None);
        _target.Change.Should().BeNull();
    }

    [Fact]
    public void GIVEN_CommitOwnedPaths_WHEN_BufferedEventsAreProcessed_THEN_ShouldIgnoreThosePaths()
    {
        var documentPath = Path.Combine(_workspaceRoot, "Document.cs");
        var createdPath = Path.Combine(_workspaceRoot, "Project", "Created.cs");
        var ignoredPaths = new HashSet<string>(StringComparer.Ordinal)
        {
            documentPath,
            createdPath,
        };

        _target.Start();
        var args = new FileSystemEventArgs(WatcherChangeTypes.Changed, _workspaceRoot, "Document.cs");
        _watcher.Raise(item => item.Changed += null, args);

        RaiseCreated(_workspaceRoot, ".Document.cs.11111111111111111111111111111111.tmp");
        RaiseCreated(Path.Combine(_workspaceRoot, "Project"), "Created.cs");
        TrackWorkspaceInputs(ignoredPaths);

        _target.WaitForPendingEvents(CancellationToken.None);
        _target.Change.Should().BeNull();
    }

    [Fact]
    public void GIVEN_UnconfiguredMonitor_WHEN_PathIsCreated_THEN_ShouldIgnoreChange()
    {
        RaiseCreated(_workspaceRoot, "Document.cs");

        _target.Change.Should().BeNull();
    }

    [Fact]
    public void GIVEN_ConfiguredMonitor_WHEN_TrackedDirectoryMetadataChanges_THEN_ShouldIgnoreChange()
    {
        TrackWorkspaceInputs();

        _watcher.Raise(
            item => item.Changed += null,
            new FileSystemEventArgs(WatcherChangeTypes.Changed, _workspaceRoot, "Project"));

        _target.WaitForPendingEvents(CancellationToken.None);
        _target.Change.Should().BeNull();
    }

    [Theory]
    [InlineData("Document.cs", true)]
    [InlineData("Untracked.cs", false)]
    [InlineData("Other/Generated.cs", false)]
    public void GIVEN_ConfiguredMonitor_WHEN_FileChanges_THEN_ShouldReportOnlyTrackedRelevantChanges(
        string relativePath,
        bool expectedChange)
    {
        TrackWorkspaceInputs();
        var directory = Path.GetDirectoryName(relativePath);
        var name = Path.GetFileName(relativePath);
        var eventDirectory = string.IsNullOrWhiteSpace(directory)
            ? _workspaceRoot
            : Path.Combine(_workspaceRoot, directory);

        _watcher.Raise(
            item => item.Changed += null,
            new FileSystemEventArgs(WatcherChangeTypes.Changed, eventDirectory, name));

        _target.WaitForPendingEvents(CancellationToken.None);
        (_target.Change is not null).Should().Be(expectedChange);
    }

    [Theory]
    [InlineData("Project/New.cs", true)]
    [InlineData("Other/New.cs", false)]
    [InlineData("Other/Nested/New.cs", false)]
    public void GIVEN_ConfiguredMonitor_WHEN_PathIsCreatedOrDeleted_THEN_ShouldReportRelevantDirectoryChanges(
        string relativePath,
        bool expectedChange)
    {
        TrackWorkspaceInputs();
        var directory = Path.GetDirectoryName(relativePath)!;
        var name = Path.GetFileName(relativePath);

        RaiseCreated(Path.Combine(_workspaceRoot, directory), name);

        _target.WaitForPendingEvents(CancellationToken.None);
        (_target.Change is not null).Should().Be(expectedChange);
    }

    [Fact]
    public void GIVEN_ArtifactRoot_WHEN_UnknownPathIsCreated_THEN_ShouldIgnoreChange()
    {
        var artifactRoot = Path.Combine(_workspaceRoot, "Project", "custom-obj");
        using var manifest = new WorkspaceInputManifest
        {
            Directories =
            [
                new WorkspaceInputDirectoryFingerprint
                {
                    Path = Path.Combine(_workspaceRoot, "Project"),
                },
            ],
            PathPolicy = WorkspaceInputPathPolicy.Create(
                [artifactRoot],
                [Path.Combine(_workspaceRoot, "Project", "Project.csproj")],
                StringComparison.Ordinal),
        };

        _target.Track(manifest);
        RaiseCreated(Path.Combine(_workspaceRoot, "Project"), "custom-obj");

        _target.WaitForPendingEvents(CancellationToken.None);
        _target.Change.Should().BeNull();
    }

    [Fact]
    public void GIVEN_TrackedInputInsideArtifactRoot_WHEN_FileChanges_THEN_ShouldReportChange()
    {
        var artifactRoot = Path.Combine(_workspaceRoot, "Project", "custom-obj");
        var trackedInput = Path.Combine(artifactRoot, "Project.nuget.g.props");
        using var manifest = new WorkspaceInputManifest
        {
            Files =
            [
                new WorkspaceInputFileFingerprint
                {
                    Path = trackedInput,
                },
            ],
            PathPolicy = WorkspaceInputPathPolicy.Create(
                [artifactRoot],
                [Path.Combine(_workspaceRoot, "Project", "Project.csproj")],
                StringComparison.Ordinal),
        };

        _target.Track(manifest);
        _watcher.Raise(
            item => item.Changed += null,
            new FileSystemEventArgs(
                WatcherChangeTypes.Changed,
                Path.GetDirectoryName(trackedInput)!,
                Path.GetFileName(trackedInput)));

        WaitForChange();
        _target.Change.Should().BeEquivalentTo(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Changed,
            Path = trackedInput,
        });
    }

    [Fact]
    public void GIVEN_ConfiguredMonitor_WHEN_TrackedPathIsRenamed_THEN_ShouldReportChange()
    {
        TrackWorkspaceInputs();
        var args = new RenamedEventArgs(
            WatcherChangeTypes.Renamed,
            _workspaceRoot,
            "Renamed.cs",
            "Document.cs");

        _watcher.Raise(item => item.Renamed += null, args);

        WaitForChange();
        _target.Change.Should().BeEquivalentTo(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Renamed,
            Path = Path.Combine(_workspaceRoot, "Renamed.cs"),
            PreviousPath = Path.Combine(_workspaceRoot, "Document.cs"),
        });
    }

    [Fact]
    public void GIVEN_ConfiguredMonitor_WHEN_TrackedPathIsDeleted_THEN_ShouldReportChange()
    {
        TrackWorkspaceInputs();
        _watcher.Raise(
            item => item.Deleted += null,
            new FileSystemEventArgs(
                WatcherChangeTypes.Deleted,
                _workspaceRoot,
                "Document.cs"));

        WaitForChange();
        _target.Change.Should().BeEquivalentTo(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Deleted,
            Path = Path.Combine(_workspaceRoot, "Document.cs"),
        });
    }

    [Fact]
    public void GIVEN_WatcherError_WHEN_Raised_THEN_ShouldReportChange()
    {
        TrackWorkspaceInputs();
        _watcher.Raise(
            item => item.Error += null,
            new ErrorEventArgs(new InternalBufferOverflowException()));

        WaitForChange();
        _target.Change.Should().BeEquivalentTo(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            ErrorCode = WorkspaceInputChangeErrorCode.WatcherBufferOverflow,
            Kind = WorkspaceInputChangeKind.WatcherError,
        });
    }

    [Fact]
    public void GIVEN_UnexpectedWatcherError_WHEN_Raised_THEN_ShouldReportGenericFailure()
    {
        TrackWorkspaceInputs();
        _watcher.Raise(
            item => item.Error += null,
            new ErrorEventArgs(new IOException()));

        WaitForChange();
        _target.Change.Should().BeEquivalentTo(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            ErrorCode = WorkspaceInputChangeErrorCode.WatcherFailure,
            Kind = WorkspaceInputChangeKind.WatcherError,
        });
    }

    [Fact]
    public void GIVEN_MultipleRelevantEvents_WHEN_Raised_THEN_ShouldRetainFirstChange()
    {
        TrackWorkspaceInputs();
        var projectDirectory = Path.Combine(_workspaceRoot, "Project");
        RaiseCreated(projectDirectory, "First.cs");
        RaiseCreated(projectDirectory, "Second.cs");

        WaitForChange();
        _target.Change.Should().BeEquivalentTo(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Created,
            Path = Path.Combine(projectDirectory, "First.cs"),
        });
    }

    [Fact]
    public void GIVEN_Monitor_WHEN_Disposed_THEN_ShouldDisposeWatcher()
    {
        _target.Dispose();

        _watcher.Verify(item => item.Dispose(), Times.Once);
    }

    public void Dispose()
    {
        _target.Dispose();
    }

    private void TrackWorkspaceInputs(IReadOnlySet<string>? ignoredPaths = null)
    {
        using var manifest = new WorkspaceInputManifest
        {
            Directories =
            [
                new WorkspaceInputDirectoryFingerprint
                {
                    Path = Path.Combine(_workspaceRoot, "Project"),
                },
            ],
            Files =
            [
                new WorkspaceInputFileFingerprint
                {
                    Path = Path.Combine(_workspaceRoot, "Document.cs"),
                },
            ],
            IgnoredPaths = ignoredPaths ?? new HashSet<string>(),
        };

        _target.Track(manifest);
        _watcher.VerifySet(item => item.EnableRaisingEvents = true, Times.Once);
    }

    private void RaiseCreated(string directory, string name)
    {
        _watcher.Raise(
            item => item.Created += null,
            new FileSystemEventArgs(WatcherChangeTypes.Created, directory, name));
    }

    private void WaitForChange()
    {
        _target.WaitForPendingEvents(CancellationToken.None);
        _target.Change.Should().NotBeNull();
    }
}
