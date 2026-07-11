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
    }

    [Fact]
    public async Task GIVEN_Workspace_WHEN_OpeningUpdatingAndClosing_THEN_ShouldPublishAndRemoveOwnedHint()
    {
        var stream = new MemoryStream();
        _directory.Setup(item => item.EnumerateFiles(It.IsAny<string>(), "*.json")).Returns([]);
        _streams.Setup(item => item.New(It.IsAny<string>(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            .Returns(new Mock<FileSystemStream>(stream, "/workspace/instance.json", false) { CallBase = true }.Object);
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object);

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
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object);

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
        using var target = new WorkspaceInstanceStatusPublisher(_fileSystem.Object);

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
}
