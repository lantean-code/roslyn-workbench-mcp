namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceCommitLockManagerTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IPath> _path;
    private readonly Mock<IWorkspaceFileLockProvider> _provider;
    private readonly Mock<IPhysicalPathContainment> _pathContainment;
    private readonly WorkspaceCommitLockManager _target;

    public WorkspaceCommitLockManagerTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _directory = new Mock<IDirectory>();
        _path = new Mock<IPath>();
        _provider = new Mock<IWorkspaceFileLockProvider>();
        _pathContainment = new Mock<IPhysicalPathContainment>();
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path.Setup(item => item.GetFullPath("Root")).Returns("Root");
        _path.Setup(item => item.Combine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string first, string second, string third, string fourth) => $"{first}/{second}/{third}/{fourth}");

        _path.Setup(item => item.Combine(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string first, string second) => $"{first}/{second}");

        _path.Setup(item => item.GetDirectoryName(It.IsAny<string>()))
            .Returns((string path) => path[..path.LastIndexOf('/')]);

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

        _target = new WorkspaceCommitLockManager(
            _fileSystem.Object,
            _provider.Object,
            _pathContainment.Object);
    }

    [Fact]
    public void GIVEN_AvailableLock_WHEN_Acquiring_THEN_ShouldReturnOwnershipFromRepositoryCoordinationDirectory()
    {
        var expectedRoot = OperatingSystem.IsWindows() ? "ROOT" : "Root";
        var ownership = new Mock<IWorkspaceCommitLock>();
        _provider.Setup(item => item.TryAcquire($"{expectedRoot}/.vs/roslyn-workbench-mcp/locks/commit.lock"))
            .Returns(ownership.Object);

        var result = _target.Acquire("Root");

        result.Status.Should().Be(WorkspaceCommitLockAcquisitionStatus.Acquired);
        result.Lock.Should().BeSameAs(ownership.Object);
        _directory.Verify(item => item.CreateDirectory($"{expectedRoot}/.vs/roslyn-workbench-mcp/locks"), Times.Once);
    }

    [Fact]
    public void GIVEN_OwnedLock_WHEN_Acquiring_THEN_ShouldReportContention()
    {
        _provider.Setup(item => item.TryAcquire(It.IsAny<string>())).Returns((IWorkspaceCommitLock?)null);

        var result = _target.Acquire("Root");

        result.Status.Should().Be(WorkspaceCommitLockAcquisitionStatus.Contended);
        result.Lock.Should().BeNull();
    }

    [Fact]
    public void GIVEN_LockPathPhysicallyEscapesWorkspace_WHEN_Acquiring_THEN_ShouldReportFailure()
    {
        _pathContainment
            .Setup(item => item.TryGetStrictlyContainedPath(
                It.IsAny<string>(),
                It.IsAny<string>(),
                out It.Ref<string>.IsAny))
            .Returns(false);

        var result = _target.Acquire("Root");

        result.Status.Should().Be(WorkspaceCommitLockAcquisitionStatus.Failed);
        result.ErrorMessage.Should().Contain("outside the workspace root");
        _directory.Verify(item => item.CreateDirectory(It.IsAny<string>()), Times.Never);
        _provider.Verify(item => item.TryAcquire(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_CoordinationIoFailure_WHEN_Acquiring_THEN_ShouldReportFailure(bool accessDenied)
    {
        if (accessDenied)
        {
            _directory.Setup(item => item.CreateDirectory(It.IsAny<string>())).Throws(new UnauthorizedAccessException("denied"));
        }
        else
        {
            _directory.Setup(item => item.CreateDirectory(It.IsAny<string>())).Throws(new IOException("failed"));
        }

        var result = _target.Acquire("Root");

        result.Status.Should().Be(WorkspaceCommitLockAcquisitionStatus.Failed);
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_LockProviderFailure_WHEN_Acquiring_THEN_ShouldReportFailure(bool accessDenied)
    {
        Exception exception;
        if (accessDenied)
        {
            exception = new UnauthorizedAccessException("denied");
        }
        else
        {
            exception = new IOException("failed");
        }

        _provider.Setup(item => item.TryAcquire(It.IsAny<string>())).Throws(exception);

        var result = _target.Acquire("Root");

        result.Status.Should().Be(WorkspaceCommitLockAcquisitionStatus.Failed);
        result.ErrorMessage.Should().Be(exception.Message);
    }
}
