using System.Text;
using System.Text.Json;
using Roslyn.Workbench.Mcp.Workspace.Coordination;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Coordination;

public sealed class WorkspaceInstanceStatusPublisherTests
{
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IDirectory> _directory = new();
    private readonly Mock<IFile> _file = new();
    private readonly Mock<IFileStreamFactory> _streams = new();
    private readonly Mock<IPath> _path = new();
    private readonly Mock<IWorkspacePathComparison> _pathComparison = new();

    public WorkspaceInstanceStatusPublisherTests()
    {
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.File).Returns(_file.Object);
        _fileSystem.SetupGet(item => item.FileStream).Returns(_streams.Object);
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path.Setup(item => item.GetFullPath(It.IsAny<string>())).Returns((string path) => path);
        _path.Setup(item => item.GetDirectoryName(It.IsAny<string>())).Returns("/workspace");
        _path.Setup(item => item.Combine(It.IsAny<string>(), It.IsAny<string>())).Returns((string left, string right) => $"{left}/{right}");
        _path.Setup(item => item.Combine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string a, string b, string c, string d) => $"{a}/{b}/{c}/{d}");
        _pathComparison.SetupGet(item => item.Comparison).Returns(StringComparison.Ordinal);
    }

    [Fact]
    public async Task GIVEN_Workspace_WHEN_OpeningUpdatingAndClosing_THEN_ShouldPublishAndRemoveOwnedHint()
    {
        var stream = new MemoryStream();
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream, "/workspace/instance.json", false) { CallBase = true }.Object);
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var warning = await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);
        await target.UpdateAsync("workspace", WorkspaceLifecycleState.TransactionActive, 2, "commit", "Applying");

        warning.Should().BeFalse();
        Encoding.UTF8.GetString(stream.ToArray()).Should().Contain("\"transactionRevision\":2").And.Contain("\"commitPhase\":\"Applying\"");
        target.Close("workspace");
        _file.Verify(item => item.Delete(It.Is<string>(path => path.EndsWith("-workspace.json", StringComparison.Ordinal))), Times.Once);
    }

    [Fact]
    public async Task GIVEN_LiveHint_WHEN_Opening_THEN_ShouldWarnWithoutTreatingHintAsRecoveryEvidence()
    {
        var stream = new MemoryStream();
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns(["/workspace/live.json"]);
        _streams.Setup(item => item.New("/workspace/live.json", FileMode.Open, FileAccess.ReadWrite, FileShare.None)).Throws(new IOException());
        _file.Setup(item => item.ReadAllTextAsync("/workspace/live.json", It.IsAny<CancellationToken>())).ReturnsAsync("{");
        _streams.Setup(item => item.New(It.Is<string>(path => path != "/workspace/live.json"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream, "/workspace/instance.json", false) { CallBase = true }.Object);
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var warning = await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        warning.Should().BeTrue();
        _file.Verify(item => item.Delete("/workspace/live.json"), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LiveValidAndStaleHints_WHEN_Querying_THEN_ShouldReturnStructuredLiveInstanceAndRemoveStaleFile()
    {
        const string livePath = "/workspace/live.json";
        const string stalePath = "/workspace/stale.json";
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([stalePath, livePath]);
        _streams.Setup(item => item.New(stalePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            .Returns(new Mock<FileSystemStream>(new MemoryStream(), stalePath, false) { CallBase = true }.Object);
        _streams.Setup(item => item.New(livePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)).Throws(new IOException());
        _file.Setup(item => item.ReadAllTextAsync(livePath, It.IsAny<CancellationToken>())).ReturnsAsync(JsonSerializer.Serialize(new WorkspaceInstanceStatus
        {
            InstanceId = "other-instance",
            LoadedPath = "/workspace/solution.slnx",
            WorkspaceRoot = "/workspace",
            WorkspaceState = WorkspaceLifecycleState.TransactionActive,
            TransactionRevision = 3,
            CommitId = "commit",
            CommitPhase = "Applying",
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var instances = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        instances.Should().ContainSingle().Which.Should().BeEquivalentTo(new WorkspaceInstanceInfo
        {
            InstanceId = "other-instance",
            LoadedPath = "/workspace/solution.slnx",
            WorkspaceRoot = "/workspace",
            WorkspaceState = WorkspaceLifecycleState.TransactionActive,
            TransactionRevision = 3,
            CommitId = "commit",
            CommitPhase = "Applying",
        });
        _file.Verify(item => item.Delete(stalePath), Times.Once);
    }

    [Fact]
    public async Task GIVEN_InstanceDirectoryDoesNotExist_WHEN_Querying_THEN_ShouldReturnNoInstances()
    {
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(false);
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var instances = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        instances.Should().BeEmpty();
        _directory.Verify(item => item.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_WhitespaceWorkspaceRoot_WHEN_Querying_THEN_ShouldRejectTheRequest()
    {
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var act = async () => await target.GetOtherLiveInstancesAsync(" ", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GIVEN_FileSystemIOException_WHEN_Opening_THEN_ShouldSuppressTheFailure()
    {
        _directory.Setup(item => item.CreateDirectory(It.IsAny<string>())).Throws(new IOException());
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var warning = await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        warning.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_FileSystemAccessFailure_WHEN_Opening_THEN_ShouldSuppressTheFailure()
    {
        _directory.Setup(item => item.CreateDirectory(It.IsAny<string>())).Throws(new UnauthorizedAccessException());
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var warning = await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        warning.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_FileSystemIOException_WHEN_Querying_THEN_ShouldReturnNoInstances()
    {
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Throws(new IOException());
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var instances = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        instances.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_FileSystemAccessFailure_WHEN_Querying_THEN_ShouldReturnNoInstances()
    {
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Throws(new UnauthorizedAccessException());
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var instances = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        instances.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_UnknownWorkspace_WHEN_UpdatingAndClosing_THEN_ShouldDoNothing()
    {
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        await target.UpdateAsync("workspace", WorkspaceLifecycleState.Ready, null, null, null);
        target.Close("workspace");

        _file.Verify(item => item.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PublishedWorkspaceAndWriteIOException_WHEN_Updating_THEN_ShouldRetainTheLeaseForClosing()
    {
        var stream = new Mock<Stream>();
        stream.SetupProperty(item => item.Position);
        stream.Setup(item => item.CanWrite).Returns(true);
        stream.Setup(item => item.SetLength(It.IsAny<long>()));
        stream.Setup(item => item.WriteAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        stream.Setup(item => item.FlushAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream.Object, "/workspace/instance.json", false) { CallBase = true }.Object);
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);
        await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);
        stream.Setup(item => item.SetLength(0)).Throws(new IOException());

        await target.UpdateAsync("workspace", WorkspaceLifecycleState.TransactionActive, 2, "commit", "Applying");
        target.Close("workspace");

        _file.Verify(item => item.Delete(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PublishedWorkspaceAndWriteAccessFailure_WHEN_Updating_THEN_ShouldRetainTheLeaseForClosing()
    {
        var stream = new Mock<Stream>();
        stream.SetupProperty(item => item.Position);
        stream.Setup(item => item.CanWrite).Returns(true);
        stream.Setup(item => item.SetLength(It.IsAny<long>()));
        stream.Setup(item => item.WriteAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        stream.Setup(item => item.FlushAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream.Object, "/workspace/instance.json", false) { CallBase = true }.Object);
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);
        await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);
        stream.Setup(item => item.SetLength(0)).Throws(new UnauthorizedAccessException());

        await target.UpdateAsync("workspace", WorkspaceLifecycleState.TransactionActive, 2, "commit", "Applying");
        target.Close("workspace");

        _file.Verify(item => item.Delete(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PublishedWorkspaceAndDeleteIOException_WHEN_Closing_THEN_ShouldSuppressTheFailure()
    {
        var stream = new MemoryStream();
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream, "/workspace/instance.json", false) { CallBase = true }.Object);
        _file.Setup(item => item.Delete(It.IsAny<string>())).Throws(new IOException());
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);
        await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        target.Close("workspace");

        stream.CanRead.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_PublishedWorkspaceAndDeleteAccessFailure_WHEN_Closing_THEN_ShouldSuppressTheFailure()
    {
        var stream = new MemoryStream();
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream, "/workspace/instance.json", false) { CallBase = true }.Object);
        _file.Setup(item => item.Delete(It.IsAny<string>())).Throws(new UnauthorizedAccessException());
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);
        await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        target.Close("workspace");

        stream.CanRead.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_MultiplePublishedWorkspaces_WHEN_Disposing_THEN_ShouldCloseEveryLease()
    {
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns((string path, FileMode _, FileAccess _, FileShare _) =>
                new Mock<FileSystemStream>(new MemoryStream(), path, false) { CallBase = true }.Object);
        var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);
        await target.OpenAsync("workspace-one", "/workspace", "/workspace/one.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);
        await target.OpenAsync("workspace-two", "/workspace", "/workspace/two.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        target.Dispose();

        _file.Verify(item => item.Delete(It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GIVEN_CancelledScan_WHEN_Querying_THEN_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns(["/workspace/live.json"]);
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var act = async () => await target.GetOtherLiveInstancesAsync("/workspace", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_LiveHintsWithDifferentStatusValidity_WHEN_Querying_THEN_ShouldReturnOnlyMatchingVersionAndRootInOrdinalOrder()
    {
        var statuses = new Dictionary<string, WorkspaceInstanceStatus>
        {
            ["/workspace/b.json"] = CreateStatus("b-instance", "/workspace", 2),
            ["/workspace/a.json"] = CreateStatus("a-instance", "/WORKSPACE", 2),
            ["/workspace/legacy.json"] = CreateStatus("legacy-instance", "/workspace", 1),
        };
        _pathComparison.SetupGet(item => item.Comparison).Returns(StringComparison.OrdinalIgnoreCase);
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns(statuses.Keys);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.Open, FileAccess.ReadWrite, FileShare.None)).Throws(new IOException());
        _file.Setup(item => item.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => JsonSerializer.Serialize(statuses[path], new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var instances = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        instances.Select(instance => instance.InstanceId).Should().Equal("a-instance", "b-instance");
    }

    [Fact]
    public async Task GIVEN_LiveHintCannotBeOpenedOrRead_WHEN_Querying_THEN_ShouldReportLiveWithoutStructuredDetails()
    {
        const string path = "/workspace/live.json";
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([path]);
        _streams.Setup(item => item.New(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)).Throws(new UnauthorizedAccessException());
        _file.Setup(item => item.ReadAllTextAsync(path, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var instances = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        instances.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_LiveHintCannotBeReadBecauseAccessIsDenied_WHEN_Querying_THEN_ShouldReportNoStructuredDetails()
    {
        const string path = "/workspace/live.json";
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([path]);
        _streams.Setup(item => item.New(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)).Throws(new IOException());
        _file.Setup(item => item.ReadAllTextAsync(path, It.IsAny<CancellationToken>())).ThrowsAsync(new UnauthorizedAccessException());
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);

        var instances = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        instances.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_OwnLiveHint_WHEN_Querying_THEN_ShouldExcludeIt()
    {
        var statusStream = new MemoryStream();
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(false);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(statusStream, "/workspace/instance.json", false) { CallBase = true }.Object);
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object, _pathComparison.Object);
        await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);
        var status = JsonSerializer.Deserialize<WorkspaceInstanceStatus>(statusStream.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        status.Should().NotBeNull();
        var path = $"/workspace/.vs/roslyn-workbench-mcp/instances/{status.InstanceId}-workspace.json";
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([path]);
        _streams.Setup(item => item.New(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)).Throws(new IOException());
        _file.Setup(item => item.ReadAllTextAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetString(statusStream.ToArray()));

        var instances = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        instances.Should().BeEmpty();
        _file.Verify(item => item.ReadAllTextAsync(path, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static WorkspaceInstanceStatus CreateStatus(string instanceId, string workspaceRoot, int version)
    {
        return new WorkspaceInstanceStatus
        {
            Version = version,
            InstanceId = instanceId,
            LoadedPath = $"{workspaceRoot}/solution.slnx",
            WorkspaceRoot = workspaceRoot,
            WorkspaceState = WorkspaceLifecycleState.Ready,
        };
    }
}
