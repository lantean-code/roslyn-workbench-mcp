using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Configuration;
using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Recovery;

public sealed class CommitRecoveryStoreTests
{
    private const string _recoveryDirectory = "/State/recovery";

    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IFile> _file;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IPath> _path;
    private readonly Mock<IAtomicFileWriter> _atomicFileWriter;
    private readonly CommitRecoveryStore _target;

    public CommitRecoveryStoreTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _file = new Mock<IFile>();
        _directory = new Mock<IDirectory>();
        _path = new Mock<IPath>();
        _atomicFileWriter = new Mock<IAtomicFileWriter>();
        _fileSystem.SetupGet(item => item.File).Returns(_file.Object);
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path.Setup(item => item.GetFullPath("StateDirectory")).Returns("/State");
        _path.Setup(item => item.GetRelativePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string root, string path) => Path.GetRelativePath(root, path));
        _path.Setup(item => item.IsPathRooted(It.IsAny<string>())).Returns((string path) => Path.IsPathRooted(path));
        _path.SetupGet(item => item.DirectorySeparatorChar).Returns(Path.DirectorySeparatorChar);
        _path.Setup(item => item.Combine("/State", "recovery")).Returns(_recoveryDirectory);
        _path
            .Setup(item => item.Combine(_recoveryDirectory, It.IsAny<string>()))
            .Returns((string _, string fileName) => _recoveryDirectory + "/" + fileName);
        _path
            .Setup(item => item.Combine(It.Is<string>(value => value.StartsWith(_recoveryDirectory, StringComparison.Ordinal)), It.IsAny<string>()))
            .Returns((string directory, string fileName) => directory + "/" + fileName);
        _target = new CommitRecoveryStore(
            Options.Create(new WorkspaceCoordinatorOptions { StateDirectory = "StateDirectory" }),
            _fileSystem.Object,
            _atomicFileWriter.Object);
    }

    [Fact]
    public void GIVEN_NullOptions_WHEN_Constructing_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => new CommitRecoveryStore(null!, _fileSystem.Object, _atomicFileWriter.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_NullFileSystem_WHEN_Constructing_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => new CommitRecoveryStore(
            Options.Create(new WorkspaceCoordinatorOptions()),
            null!,
            _atomicFileWriter.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_NullAtomicFileWriter_WHEN_Constructing_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => new CommitRecoveryStore(
            Options.Create(new WorkspaceCoordinatorOptions()),
            _fileSystem.Object,
            null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GIVEN_MissingRecoveryDirectory_WHEN_ReadingStatuses_THEN_ShouldReturnEmptyCollection()
    {
        var result = await _target.GetStatusesAsync(TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _directory.Verify(item => item.EnumerateFiles(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<SearchOption>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ValidAndUnreadableRecords_WHEN_ReadingStatuses_THEN_ShouldReturnOnlyValidStatus()
    {
        var validPath = _recoveryDirectory + "/valid.json";
        var nullPath = _recoveryDirectory + "/null.json";
        var malformedPath = _recoveryDirectory + "/malformed.json";
        var ioFailurePath = _recoveryDirectory + "/io.json";
        var accessFailurePath = _recoveryDirectory + "/access.json";
        _directory.Setup(item => item.Exists(_recoveryDirectory)).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(_recoveryDirectory, "*.json", SearchOption.TopDirectoryOnly)).Returns(
            [validPath, nullPath, malformedPath, ioFailurePath, accessFailurePath]);
        _file.Setup(item => item.ReadAllTextAsync(validPath, TestContext.Current.CancellationToken)).ReturnsAsync(
            JsonSerializer.Serialize(new RecoveryStatus { CommitId = "CommitId", SolutionPath = "SolutionPath" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        _file.Setup(item => item.ReadAllTextAsync(nullPath, TestContext.Current.CancellationToken)).ReturnsAsync("null");
        _file.Setup(item => item.ReadAllTextAsync(malformedPath, TestContext.Current.CancellationToken)).ReturnsAsync("{");
        _file.Setup(item => item.ReadAllTextAsync(ioFailurePath, TestContext.Current.CancellationToken)).ThrowsAsync(new IOException());
        _file.Setup(item => item.ReadAllTextAsync(accessFailurePath, TestContext.Current.CancellationToken)).ThrowsAsync(new UnauthorizedAccessException());

        var result = await _target.GetStatusesAsync(TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].CommitId.Should().Be("CommitId");
        result[0].SolutionPath.Should().Be("SolutionPath");
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_ReadingStatuses_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        _directory.Setup(item => item.Exists(_recoveryDirectory)).Returns(true);
        _directory.Setup(item => item.EnumerateFiles(_recoveryDirectory, "*.json", SearchOption.TopDirectoryOnly)).Returns(["StatusPath"]);

        var action = async () => await _target.GetStatusesAsync(cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _file.Verify(item => item.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_Status_WHEN_Writing_THEN_ShouldCreateConfiguredDirectoryAndDelegateAtomicWrite()
    {
        var status = new RecoveryStatus
        {
            CommitId = "CommitId",
            SolutionPath = "SolutionPath",
            State = RecoveryState.Applying,
        };

        await _target.WriteStatusAsync(status, TestContext.Current.CancellationToken);

        _directory.Verify(item => item.CreateDirectory(_recoveryDirectory), Times.Once);
        _atomicFileWriter.Verify(item => item.WriteAllTextAsync(
            _recoveryDirectory + "/CommitId.json",
            It.Is<string>(json => json.Contains("CommitId", StringComparison.Ordinal) && json.Contains("SolutionPath", StringComparison.Ordinal)),
            It.IsAny<Encoding>(),
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_NullStatus_WHEN_Writing_THEN_ShouldThrowArgumentNullException()
    {
        var action = async () => await _target.WriteStatusAsync(null!, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_Writing_THEN_ShouldPropagateCancellationWithoutCreatingDirectory()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.WriteStatusAsync(new RecoveryStatus(), cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _directory.Verify(item => item.CreateDirectory(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GIVEN_InvalidCommitId_WHEN_Deleting_THEN_ShouldThrowArgumentException(string commitId)
    {
        var action = () => _target.DeleteStatus(commitId);

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_StatusPath_WHEN_Deleting_THEN_ShouldDeleteOnlyExistingRecord(bool exists)
    {
        var path = _recoveryDirectory + "/CommitId.json";
        _file.Setup(item => item.Exists(path)).Returns(exists);

        _target.DeleteStatus("CommitId");

        _file.Verify(item => item.Delete(path), exists ? Times.Once() : Times.Never());
    }
}
