using System.Text;
using System.Text.Json;
using Moq.Protected;
using Roslyn.Workbench.Mcp.Workspace.Coordination;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Coordination;

#pragma warning disable CA1869 // Fresh mutable options instances keep serialization scenarios isolated from one another.
public sealed class WorkspaceInstanceStatusPublisherTests
{
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IDirectory> _directory = new();
    private readonly Mock<IFile> _file = new();
    private readonly Mock<IFileStreamFactory> _streams = new();
    private readonly Mock<IPath> _path = new();
    private readonly Mock<IWorkspacePathComparison> _pathComparison = new();
    private readonly Mock<IPhysicalPathContainment> _pathContainment = new();

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
        _pathComparison.Setup(item => item.GetComparison(It.IsAny<string>())).Returns(StringComparison.Ordinal);
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
    }

    [Fact]
    public async Task GIVEN_Workspace_WHEN_OpeningUpdatingAndClosing_THEN_ShouldPublishAndRemoveOwnedHint()
    {
        var stream = new MemoryStream();
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream, "/workspace/instance.json", false) { CallBase = true }.Object);

        await using var target = CreateTarget();

        var result = await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);
        await target.UpdateAsync("workspace", WorkspaceLifecycleState.TransactionActive, 2, "commit", "Applying");

        result.Should().BeSameAs(WorkspaceInstanceStatusResult.Empty);
        Encoding.UTF8.GetString(stream.ToArray()).Should().Contain("\"transactionRevision\":2").And.Contain("\"commitPhase\":\"Applying\"");
        await target.CloseAsync("workspace");
        _file.Verify(item => item.Delete(It.Is<string>(path => path.EndsWith("-workspace.json", StringComparison.Ordinal))), Times.Once);
    }

    [Fact]
    public async Task GIVEN_InstanceDirectoryPhysicallyEscapesWorkspace_WHEN_Opening_THEN_ShouldReturnUnavailable()
    {
        _pathContainment
            .Setup(item => item.TryGetStrictlyContainedPath(
                It.IsAny<string>(),
                It.IsAny<string>(),
                out It.Ref<string>.IsAny))
            .Returns(false);

        await using var target = CreateTarget();

        var result = await target.OpenAsync(
            "workspace",
            "/workspace",
            "/workspace/solution.slnx",
            WorkspaceLifecycleState.Ready,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(WorkspaceInstanceStatusResult.Unavailable);
        _directory.Verify(item => item.CreateDirectory(It.IsAny<string>()), Times.Never);
        _streams.Verify(
            item => item.New(
                It.IsAny<string>(),
                It.IsAny<FileMode>(),
                It.IsAny<FileAccess>(),
                It.IsAny<FileShare>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_LiveHint_WHEN_Opening_THEN_ShouldWarnWithoutTreatingHintAsRecoveryEvidence()
    {
        var stream = new MemoryStream();
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns(["/workspace/live.json"]);
        _streams.Setup(item => item.New("/workspace/live.json", FileMode.Open, FileAccess.ReadWrite, FileShare.None)).Throws(new IOException());
        SetupReadableStatus("/workspace/live.json", "{");
        _streams.Setup(item => item.New(It.Is<string>(path => path != "/workspace/live.json"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream, "/workspace/instance.json", false) { CallBase = true }.Object);

        await using var target = CreateTarget();

        var result = await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        result.HasOtherLiveInstance.Should().BeTrue();
        result.HasUnreadableLiveInstance.Should().BeTrue();
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
        SetupReadableStatus(livePath, JsonSerializer.Serialize(new WorkspaceInstanceStatus
        {
            InstanceId = "other-instance",
            LoadedPath = "/workspace/solution.slnx",
            WorkspaceRoot = "/workspace",
            WorkspaceState = WorkspaceLifecycleState.TransactionActive,
            TransactionRevision = 3,
            CommitId = "commit",
            CommitPhase = "Applying",
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        await using var target = CreateTarget();

        var result = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        result.Instances.Should().ContainSingle().Which.Should().BeEquivalentTo(new WorkspaceInstanceInfo
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
        await using var target = CreateTarget();

        var result = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        result.Should().BeSameAs(WorkspaceInstanceStatusResult.Empty);
        _directory.Verify(item => item.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_WhitespaceWorkspaceRoot_WHEN_Querying_THEN_ShouldRejectTheRequest()
    {
        await using var target = CreateTarget();

        var act = async () => await target.GetOtherLiveInstancesAsync(" ", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GIVEN_FileSystemIOException_WHEN_Opening_THEN_ShouldReportUnavailableStatus()
    {
        _directory.Setup(item => item.CreateDirectory(It.IsAny<string>())).Throws(new IOException());
        await using var target = CreateTarget();

        var result = await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(WorkspaceInstanceStatusResult.Unavailable);
    }

    [Fact]
    public async Task GIVEN_FileSystemAccessFailure_WHEN_Opening_THEN_ShouldReportUnavailableStatus()
    {
        _directory.Setup(item => item.CreateDirectory(It.IsAny<string>())).Throws(new UnauthorizedAccessException());
        await using var target = CreateTarget();

        var result = await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(WorkspaceInstanceStatusResult.Unavailable);
    }

    [Fact]
    public async Task GIVEN_FileSystemIOException_WHEN_Querying_THEN_ShouldReportUnavailableStatus()
    {
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Throws(new IOException());
        await using var target = CreateTarget();

        var result = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        result.Should().BeSameAs(WorkspaceInstanceStatusResult.Unavailable);
    }

    [Fact]
    public async Task GIVEN_FileSystemAccessFailure_WHEN_Querying_THEN_ShouldReportUnavailableStatus()
    {
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Throws(new UnauthorizedAccessException());
        await using var target = CreateTarget();

        var result = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        result.Should().BeSameAs(WorkspaceInstanceStatusResult.Unavailable);
    }

    [Fact]
    public async Task GIVEN_UnknownWorkspace_WHEN_UpdatingAndClosing_THEN_ShouldDoNothing()
    {
        await using var target = CreateTarget();

        await target.UpdateAsync("workspace", WorkspaceLifecycleState.Ready, null, null, null);
        await target.CloseAsync("workspace");

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

        await using var target = CreateTarget();
        await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);
        stream.Setup(item => item.SetLength(0)).Throws(new IOException());

        await target.UpdateAsync("workspace", WorkspaceLifecycleState.TransactionActive, 2, "commit", "Applying");
        await target.CloseAsync("workspace");

        _file.Verify(item => item.Delete(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PublishedWorkspace_WHEN_QueuingUpdate_THEN_ShouldPersistItBeforeDisposalCompletes()
    {
        var stream = new MemoryStream();
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream, "/workspace/instance.json", false) { CallBase = true }.Object);

        var target = CreateTarget();
        await target.OpenAsync(
            "workspace",
            "/workspace",
            "/workspace/solution.slnx",
            WorkspaceLifecycleState.Ready,
            TestContext.Current.CancellationToken);

        target.QueueUpdate(
            "workspace",
            WorkspaceLifecycleState.WorkspaceOutOfDate,
            transactionRevision: null,
            commitId: null,
            commitPhase: null);

        await target.DisposeAsync();

        var status = JsonSerializer.Deserialize<WorkspaceInstanceStatus>(
            stream.ToArray(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        status!.WorkspaceState.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
    }

    [Fact]
    public async Task GIVEN_QueuedUpdate_WHEN_AwaitingLaterUpdate_THEN_ShouldPublishInRequestOrder()
    {
        var stream = new MemoryStream();
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream, "/workspace/instance.json", false) { CallBase = true }.Object);

        await using var target = CreateTarget();
        await target.OpenAsync(
            "workspace",
            "/workspace",
            "/workspace/solution.slnx",
            WorkspaceLifecycleState.Ready,
            TestContext.Current.CancellationToken);

        target.QueueUpdate(
            "workspace",
            WorkspaceLifecycleState.WorkspaceOutOfDate,
            transactionRevision: null,
            commitId: null,
            commitPhase: null);

        await target.UpdateAsync(
            "workspace",
            WorkspaceLifecycleState.Ready,
            transactionRevision: null,
            commitId: null,
            commitPhase: null);

        var status = JsonSerializer.Deserialize<WorkspaceInstanceStatus>(
            stream.ToArray(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        status!.WorkspaceState.Should().Be(WorkspaceLifecycleState.Ready);
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

        await using var target = CreateTarget();
        await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);
        stream.Setup(item => item.SetLength(0)).Throws(new UnauthorizedAccessException());

        await target.UpdateAsync("workspace", WorkspaceLifecycleState.TransactionActive, 2, "commit", "Applying");
        await target.CloseAsync("workspace");

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
        await using var target = CreateTarget();
        await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        await target.CloseAsync("workspace");

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
        await using var target = CreateTarget();
        await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        await target.CloseAsync("workspace");

        stream.CanRead.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_MultiplePublishedWorkspaces_WHEN_Disposing_THEN_ShouldCloseEveryHandle()
    {
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns((string path, FileMode _, FileAccess _, FileShare _) =>
                new Mock<FileSystemStream>(new MemoryStream(), path, false) { CallBase = true }.Object);

        var target = CreateTarget();
        await target.OpenAsync("workspace-one", "/workspace", "/workspace/one.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);
        await target.OpenAsync("workspace-two", "/workspace", "/workspace/two.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);

        await target.DisposeAsync();

        _file.Verify(item => item.Delete(It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GIVEN_MultipleConcurrentOpensForOneWorkspace_WHEN_Publishing_THEN_ShouldCreateOneHandle()
    {
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(new MemoryStream(), "/workspace/instance.json", false) { CallBase = true }.Object);

        await using var target = CreateTarget();

        var opens = Enumerable.Range(0, 10)
            .Select(_ => target.OpenAsync(
                "workspace",
                "/workspace",
                "/workspace/solution.slnx",
                WorkspaceLifecycleState.Ready,
                TestContext.Current.CancellationToken).AsTask());

        await Task.WhenAll(opens);

        _streams.Verify(
            item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read),
            Times.Once);
    }

    [Fact]
    public async Task GIVEN_UpdateInProgress_WHEN_ClosingWorkspace_THEN_ShouldCompleteUpdateBeforeClosingHandle()
    {
        var updateStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueUpdate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new Mock<Stream>();
        stream.SetupProperty(item => item.Position);
        stream.SetupGet(item => item.CanWrite).Returns(true);
        stream.Setup(item => item.SetLength(It.IsAny<long>()));
        stream.Setup(item => item.WriteAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        stream.SetupSequence(item => item.FlushAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Returns(() =>
            {
                updateStarted.TrySetResult(true);
                return continueUpdate.Task;
            });

        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream.Object, "/workspace/instance.json", false) { CallBase = true }.Object);

        await using var target = CreateTarget();
        await target.OpenAsync(
            "workspace",
            "/workspace",
            "/workspace/solution.slnx",
            WorkspaceLifecycleState.Ready,
            TestContext.Current.CancellationToken);

        var updateTask = target.UpdateAsync("workspace", WorkspaceLifecycleState.TransactionActive, 2, "commit", "Applying").AsTask();
        await updateStarted.Task;
        var closeTask = target.CloseAsync("workspace").AsTask();

        closeTask.IsCompleted.Should().BeFalse();
        _file.Verify(item => item.Delete(It.IsAny<string>()), Times.Never);
        continueUpdate.SetResult(true);
        await updateTask;
        await closeTask;
        _file.Verify(item => item.Delete(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DisposedPublisher_WHEN_OpeningWorkspace_THEN_ShouldNotCreateAHandle()
    {
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        var target = CreateTarget();
        await target.DisposeAsync();

        var result = await target.OpenAsync(
            "workspace",
            "/workspace",
            "/workspace/solution.slnx",
            WorkspaceLifecycleState.Ready,
            TestContext.Current.CancellationToken);

        await target.UpdateAsync("workspace", WorkspaceLifecycleState.Ready, null, null, null);
        await target.DisposeAsync();

        result.Should().BeSameAs(WorkspaceInstanceStatusResult.Unavailable);
        _streams.Verify(
            item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_HandleDisposalFailure_WHEN_Disposing_THEN_ShouldDrainHandlesAndRemainDisposed()
    {
        var stream = new Mock<Stream>();
        stream.SetupProperty(item => item.Position);
        stream.SetupGet(item => item.CanWrite).Returns(true);
        stream.Setup(item => item.SetLength(It.IsAny<long>()));
        stream.Setup(item => item.WriteAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        stream.Setup(item => item.FlushAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var fileSystemStream = new Mock<FileSystemStream>(stream.Object, "/workspace/instance.json", false) { CallBase = true };
        fileSystemStream.Protected().Setup("Dispose", true, ItExpr.IsAny<bool>()).Throws(new IOException("failure"));
        var healthyStream = new Mock<FileSystemStream>(new MemoryStream(), "/workspace/healthy.json", false) { CallBase = true };
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.SetupSequence(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(fileSystemStream.Object)
            .Returns(healthyStream.Object);

        var target = CreateTarget();
        await target.OpenAsync(
            "workspace",
            "/workspace",
            "/workspace/solution.slnx",
            WorkspaceLifecycleState.Ready,
            TestContext.Current.CancellationToken);

        await target.OpenAsync(
            "healthy-workspace",
            "/workspace",
            "/workspace/healthy.slnx",
            WorkspaceLifecycleState.Ready,
            TestContext.Current.CancellationToken);

        var firstAction = async () => await target.DisposeAsync();

        await firstAction.Should().ThrowAsync<IOException>().WithMessage("failure");
        await target.DisposeAsync();
        _file.Verify(
            item => item.Delete(It.Is<string>(path => path.EndsWith("-healthy-workspace.json", StringComparison.Ordinal))),
            Times.Once);
    }

    [Fact]
    public async Task GIVEN_CancelledScan_WHEN_Querying_THEN_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns(["/workspace/live.json"]);
        await using var target = CreateTarget();

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

        _pathComparison.Setup(item => item.GetComparison(It.IsAny<string>())).Returns(StringComparison.OrdinalIgnoreCase);
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns(statuses.Keys);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.Open, FileAccess.ReadWrite, FileShare.None)).Throws(new IOException());
        foreach (var (path, status) in statuses)
        {
            SetupReadableStatus(path, JsonSerializer.Serialize(status, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }

        await using var target = CreateTarget();

        var result = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        result.Instances.Select(instance => instance.InstanceId).Should().Equal("a-instance", "b-instance");
        result.HasUnreadableLiveInstance.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_LiveHintCannotBeOpenedOrRead_WHEN_Querying_THEN_ShouldReportLiveWithoutStructuredDetails()
    {
        const string path = "/workspace/live.json";
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([path]);
        _streams.Setup(item => item.New(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)).Throws(new UnauthorizedAccessException());
        _streams.Setup(item => item.New(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)).Throws(new IOException());
        await using var target = CreateTarget();

        var result = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        result.Instances.Should().BeEmpty();
        result.HasOtherLiveInstance.Should().BeTrue();
        result.HasUnreadableLiveInstance.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_LiveHintCannotBeReadBecauseAccessIsDenied_WHEN_Querying_THEN_ShouldReportNoStructuredDetails()
    {
        const string path = "/workspace/live.json";
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([path]);
        _streams.Setup(item => item.New(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)).Throws(new IOException());
        _streams.Setup(item => item.New(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)).Throws(new UnauthorizedAccessException());
        await using var target = CreateTarget();

        var result = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        result.Instances.Should().BeEmpty();
        result.HasOtherLiveInstance.Should().BeTrue();
        result.HasUnreadableLiveInstance.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_OwnLiveHint_WHEN_Querying_THEN_ShouldExcludeIt()
    {
        var statusStream = new MemoryStream();
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(false);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(statusStream, "/workspace/instance.json", false) { CallBase = true }.Object);

        await using var target = CreateTarget();
        await target.OpenAsync("workspace", "/workspace", "/workspace/solution.slnx", WorkspaceLifecycleState.Ready, TestContext.Current.CancellationToken);
        var status = JsonSerializer.Deserialize<WorkspaceInstanceStatus>(statusStream.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        status.Should().NotBeNull();
        var path = $"/workspace/.vs/roslyn-workbench-mcp/instances/{status.InstanceId}-workspace.json";
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([path]);
        _streams.Setup(item => item.New(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)).Throws(new IOException());
        SetupReadableStatus(path, Encoding.UTF8.GetString(statusStream.ToArray()));

        var result = await target.GetOtherLiveInstancesAsync("/workspace", TestContext.Current.CancellationToken);

        result.Instances.Should().BeEmpty();
        _streams.Verify(
            item => item.New(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete),
            Times.Once);
    }

    private void SetupReadableStatus(string path, string json)
    {
        _streams.Setup(item => item.New(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            .Returns(() => new Mock<FileSystemStream>(
                new MemoryStream(Encoding.UTF8.GetBytes(json)),
                path,
                false)
            { CallBase = true }.Object);
    }

    private WorkspaceInstanceStatusPublisher CreateTarget()
    {
        return new WorkspaceInstanceStatusPublisher(
            _fileSystem.Object,
            _pathComparison.Object,
            _pathContainment.Object);
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
#pragma warning restore CA1869
