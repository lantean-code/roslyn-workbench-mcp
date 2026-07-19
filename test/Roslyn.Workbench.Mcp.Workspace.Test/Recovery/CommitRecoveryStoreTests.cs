using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Configuration;
using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Recovery;

#pragma warning disable CA1869 // Fresh mutable options instances keep recovery serialization scenarios isolated from one another.
public sealed class CommitRecoveryStoreTests
{
    private const string _recoveryDirectory = "/State/recovery";

    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IFile> _file;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IPath> _path;
    private readonly Mock<IAtomicFileWriter> _atomicFileWriter;
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly CommitRecoveryStore _target;

    public CommitRecoveryStoreTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _file = new Mock<IFile>();
        _directory = new Mock<IDirectory>();
        _path = new Mock<IPath>();
        _atomicFileWriter = new Mock<IAtomicFileWriter>();
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _fileSystem.SetupGet(item => item.File).Returns(_file.Object);
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path.Setup(item => item.GetFullPath("StateDirectory")).Returns("/State");
        _path.Setup(item => item.GetRelativePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string root, string path) => Path.GetRelativePath(root, path));
        _path.Setup(item => item.IsPathRooted(It.IsAny<string>())).Returns((string path) => Path.IsPathRooted(path));
        _path.Setup(item => item.IsPathFullyQualified(It.IsAny<string>())).Returns((string path) => Path.IsPathFullyQualified(path));
        _path.Setup(item => item.GetFileName(It.IsAny<string>())).Returns((string path) => Path.GetFileName(path));
        _path.Setup(item => item.GetFileNameWithoutExtension(It.IsAny<string>())).Returns((string path) => Path.GetFileNameWithoutExtension(path));
        _path.Setup(item => item.GetDirectoryName(It.IsAny<string>())).Returns((string path) => Path.GetDirectoryName(path));
        _path.Setup(item => item.GetInvalidFileNameChars()).Returns(['*', '/', '\\']);
        _path.SetupGet(item => item.DirectorySeparatorChar).Returns(Path.DirectorySeparatorChar);
        _path.Setup(item => item.Combine("/State", "recovery")).Returns(_recoveryDirectory);
        _path
            .Setup(item => item.Combine(_recoveryDirectory, It.IsAny<string>()))
            .Returns((string _, string fileName) => _recoveryDirectory + "/" + fileName);
        _path
            .Setup(item => item.Combine(It.Is<string>(value => value.StartsWith(_recoveryDirectory, StringComparison.Ordinal)), It.IsAny<string>()))
            .Returns((string directory, string fileName) => directory + "/" + fileName);
        _path.Setup(item => item.GetFullPath(It.Is<string>(value => value.StartsWith(_recoveryDirectory, StringComparison.Ordinal))))
            .Returns((string path) => Path.GetFullPath(path));
        _pathComparison.SetupGet(item => item.Comparison).Returns(StringComparison.Ordinal);
        _pathComparison.SetupGet(item => item.Comparer).Returns(StringComparer.Ordinal);
        _target = new CommitRecoveryStore(
            Options.Create(new WorkspaceCoordinatorOptions { StateDirectory = "StateDirectory" }),
            _fileSystem.Object,
            _atomicFileWriter.Object,
            _pathComparison.Object);
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
    public async Task GIVEN_ValidAndUnreadableLegacyRecords_WHEN_ReadingStatuses_THEN_ShouldReturnConflictForEveryRecord()
    {
        var validPath = _recoveryDirectory + "/CommitId.json";
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

        result.Should().HaveCount(5);
        result.Should().AllSatisfy(status => status.State.Should().Be(RecoveryState.RecoveryConflict));
        var validStatus = result.Single(status => status.CommitId == "CommitId");
        validStatus.SolutionPath.Should().Be("SolutionPath");
        validStatus.Message.Should().Be("Legacy recovery evidence cannot be restored automatically.");
        result.Where(status => status.CommitId != "CommitId").Should().AllSatisfy(status =>
        {
            status.SolutionPath.Should().BeEmpty();
            status.WorkspaceRoot.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_ReadingStatuses_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
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
    public async Task GIVEN_CancelledToken_WHEN_Writing_THEN_ShouldPropagateCancellationWithoutCreatingDirectory()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.WriteStatusAsync(new RecoveryStatus(), cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _directory.Verify(item => item.CreateDirectory(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../CommitId")]
    [InlineData("Invalid*CommitId")]
    public async Task GIVEN_InvalidCommitId_WHEN_WritingStatus_THEN_ShouldRejectBeforeCreatingDirectory(string commitId)
    {
        var status = new RecoveryStatus { CommitId = commitId };

        var action = async () => await _target.WriteStatusAsync(status, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
        _directory.Verify(item => item.CreateDirectory(It.IsAny<string>()), Times.Never);
        _atomicFileWriter.Verify(item => item.WriteAllTextAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Encoding>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PlanWithArtifacts_WHEN_Persisting_THEN_ShouldWriteOwnerArtifactsAndManifestInOrder()
    {
        var operations = new List<string>();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["staged/File.bin"] = new byte[] { 1, 2 },
        });
        _atomicFileWriter.Setup(item => item.WriteAllTextAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Encoding>(), It.IsAny<CancellationToken>()))
            .Callback((string path, string _, Encoding _, CancellationToken _) => operations.Add(path))
            .Returns(ValueTask.CompletedTask);
        _atomicFileWriter.Setup(item => item.WriteAllBytesAsync(
                It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback((string path, ReadOnlyMemory<byte> _, CancellationToken _) => operations.Add(path))
            .Returns(ValueTask.CompletedTask);

        await _target.PersistPlanAsync(plan, TestContext.Current.CancellationToken);

        operations.Should().Equal(
            _recoveryDirectory + "/CommitId/owner.json",
            _recoveryDirectory + "/CommitId/staged/File.bin",
            _recoveryDirectory + "/CommitId/manifest.json");
        _directory.Verify(item => item.CreateDirectory(_recoveryDirectory + "/CommitId"), Times.AtLeastOnce);
        _directory.Verify(item => item.CreateDirectory(_recoveryDirectory + "/CommitId/staged"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_Manifest_WHEN_Writing_THEN_ShouldCreateCommitDirectoryAndWriteSerializedManifest()
    {
        var manifest = CreateManifest();

        await _target.WriteManifestAsync(manifest, TestContext.Current.CancellationToken);

        _directory.Verify(item => item.CreateDirectory(_recoveryDirectory + "/CommitId"), Times.Once);
        _atomicFileWriter.Verify(item => item.WriteAllTextAsync(
            _recoveryDirectory + "/CommitId/manifest.json",
            It.Is<string>(json => json.Contains("CommitId", StringComparison.Ordinal)),
            It.IsAny<Encoding>(),
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_Artifact_WHEN_Reading_THEN_ShouldReadValidatedArtifactPath()
    {
        var expected = new byte[] { 1, 2 };
        _file.Setup(item => item.ReadAllBytesAsync(
            _recoveryDirectory + "/CommitId/staged/File.bin",
            TestContext.Current.CancellationToken)).ReturnsAsync(expected);

        var result = await _target.ReadArtifactAsync("CommitId", "staged/File.bin", TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_EmptyArtifactPath_WHEN_Reading_THEN_ShouldRejectPath()
    {
        var action = async () => await _target.ReadArtifactAsync("CommitId", string.Empty, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
        _file.Verify(item => item.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ArtifactPathTraversal_WHEN_Reading_THEN_ShouldRejectPath()
    {
        var action = async () => await _target.ReadArtifactAsync("CommitId", "../File.bin", TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidDataException>();
        _file.Verify(item => item.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ValidManifest_WHEN_ReadingManifests_THEN_ShouldReturnManifest()
    {
        var directory = _recoveryDirectory + "/CommitId";
        var path = directory + "/manifest.json";
        _directory.Setup(item => item.Exists(_recoveryDirectory)).Returns(true);
        _directory.Setup(item => item.EnumerateDirectories(_recoveryDirectory)).Returns([directory]);
        _file.Setup(item => item.Exists(path)).Returns(true);
        _file.Setup(item => item.ReadAllTextAsync(path, TestContext.Current.CancellationToken))
            .ReturnsAsync(JsonSerializer.Serialize(CreateManifest(), new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var result = await _target.GetManifestsAsync(TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.CommitId.Should().Be("CommitId");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("json")]
    [InlineData("io")]
    [InlineData("access")]
    public async Task GIVEN_MissingOrUnreadableManifest_WHEN_ReadingManifests_THEN_ShouldSkipOrReturnConflict(string scenario)
    {
        var directory = _recoveryDirectory + "/CommitId";
        var path = directory + "/manifest.json";
        _directory.Setup(item => item.Exists(_recoveryDirectory)).Returns(true);
        _directory.Setup(item => item.EnumerateDirectories(_recoveryDirectory)).Returns([directory]);
        _file.Setup(item => item.Exists(path)).Returns(scenario != "missing");
        _file.Setup(item => item.ReadAllTextAsync(path, TestContext.Current.CancellationToken))
            .Returns(() => scenario switch
            {
                "null" => Task.FromResult("null"),
                "json" => Task.FromResult("{"),
                "io" => Task.FromException<string>(new IOException()),
                "access" => Task.FromException<string>(new UnauthorizedAccessException()),
                _ => Task.FromResult(string.Empty),
            });

        var result = await _target.GetManifestsAsync(TestContext.Current.CancellationToken);

        if (scenario == "missing")
        {
            result.Should().BeEmpty();
        }
        else
        {
            result.Should().ContainSingle().Which.State.Should().Be(RecoveryState.RecoveryConflict);
        }
    }

    [Fact]
    public async Task GIVEN_CancelledManifestEnumeration_WHEN_ReadingManifests_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        _directory.Setup(item => item.Exists(_recoveryDirectory)).Returns(true);
        _directory.Setup(item => item.EnumerateDirectories(_recoveryDirectory)).Returns([_recoveryDirectory + "/CommitId"]);

        var action = async () => await _target.GetManifestsAsync(cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("version")]
    [InlineData("commit")]
    [InlineData("loaded")]
    [InlineData("root")]
    [InlineData("outside")]
    [InlineData("target")]
    [InlineData("duplicate")]
    [InlineData("delete")]
    [InlineData("backup")]
    [InlineData("backupEmpty")]
    [InlineData("staged")]
    [InlineData("artifactArgument")]
    [InlineData("artifactRelative")]
    [InlineData("invalidCommit")]
    [InlineData("emptyCommit")]
    [InlineData("created")]
    [InlineData("createdRelative")]
    public async Task GIVEN_UnsafeManifest_WHEN_ReadingManifests_THEN_ShouldReturnRecoveryConflict(string scenario)
    {
        var directory = _recoveryDirectory + "/CommitId";
        var path = directory + "/manifest.json";
        var manifest = CreateInvalidManifest(scenario);
        _directory.Setup(item => item.Exists(_recoveryDirectory)).Returns(true);
        _directory.Setup(item => item.EnumerateDirectories(_recoveryDirectory)).Returns([directory]);
        _file.Setup(item => item.Exists(path)).Returns(true);
        _file.Setup(item => item.ReadAllTextAsync(path, TestContext.Current.CancellationToken))
            .ReturnsAsync(JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        if (scenario == "artifactArgument")
        {
            _path.Setup(item => item.GetFullPath(_recoveryDirectory + "/CommitId/invalid"))
                .Throws<ArgumentException>();
        }
        else if (scenario == "artifactRelative")
        {
            _path.Setup(item => item.GetFullPath(_recoveryDirectory + "/CommitId/relative"))
                .Returns("relative");
            _path.Setup(item => item.GetRelativePath(_recoveryDirectory + "/CommitId", "relative"))
                .Returns("relative");
        }

        var result = await _target.GetManifestsAsync(TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.State.Should().Be(RecoveryState.RecoveryConflict);
    }

    [Fact]
    public async Task GIVEN_ValidOrphanOwner_WHEN_ReadingOwners_THEN_ShouldReturnOwner()
    {
        var directory = _recoveryDirectory + "/CommitId";
        var ownerPath = directory + "/owner.json";
        var owner = new WorkspaceCommitOwner { CommitId = "CommitId", LoadedPath = "/Workspace/Workspace.sln", WorkspaceRoot = "/Workspace" };
        _directory.Setup(item => item.Exists(_recoveryDirectory)).Returns(true);
        _directory.Setup(item => item.EnumerateDirectories(_recoveryDirectory)).Returns([directory]);
        _file.Setup(item => item.Exists(directory + "/manifest.json")).Returns(false);
        _file.Setup(item => item.Exists(ownerPath)).Returns(true);
        _file.Setup(item => item.ReadAllTextAsync(ownerPath, TestContext.Current.CancellationToken))
            .ReturnsAsync(JsonSerializer.Serialize(owner, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var result = await _target.GetOrphanedCommitOwnersAsync(TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(owner);
    }

    [Fact]
    public async Task GIVEN_ValidOrphanOwner_WHEN_ReadingStatuses_THEN_ShouldReturnConflictWithOwnerIdentity()
    {
        var directory = _recoveryDirectory + "/CommitId";
        var ownerPath = directory + "/owner.json";
        var owner = new WorkspaceCommitOwner { CommitId = "CommitId", LoadedPath = "/Workspace/Workspace.sln", WorkspaceRoot = "/Workspace" };
        _directory.Setup(item => item.Exists(_recoveryDirectory)).Returns(true);
        _directory.Setup(item => item.EnumerateDirectories(_recoveryDirectory)).Returns([directory]);
        _directory.Setup(item => item.EnumerateFiles(_recoveryDirectory, "*.json", SearchOption.TopDirectoryOnly)).Returns([]);
        _file.Setup(item => item.Exists(directory + "/manifest.json")).Returns(false);
        _file.Setup(item => item.Exists(ownerPath)).Returns(true);
        _file.Setup(item => item.ReadAllTextAsync(ownerPath, TestContext.Current.CancellationToken))
            .ReturnsAsync(JsonSerializer.Serialize(owner, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var result = await _target.GetStatusesAsync(TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new RecoveryStatus
        {
            CommitId = "CommitId",
            SolutionPath = "/Workspace/Workspace.sln",
            WorkspaceRoot = "/Workspace",
            State = RecoveryState.RecoveryConflict,
            Message = "The commit was interrupted before its durable manifest was prepared.",
        });
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("version")]
    [InlineData("commit")]
    [InlineData("loaded")]
    [InlineData("root")]
    [InlineData("json")]
    [InlineData("io")]
    [InlineData("access")]
    public async Task GIVEN_InvalidOrUnreadableOrphanOwner_WHEN_ReadingOwners_THEN_ShouldIgnoreOwner(string scenario)
    {
        var directory = _recoveryDirectory + "/CommitId";
        var manifestPath = directory + "/manifest.json";
        var ownerPath = directory + "/owner.json";
        var owner = new WorkspaceCommitOwner
        {
            CommitId = scenario == "commit" ? "OtherCommitId" : "CommitId",
            LoadedPath = scenario == "loaded" ? "Workspace.sln" : "/Workspace/Workspace.sln",
            WorkspaceRoot = scenario == "root" ? "Workspace" : "/Workspace",
            Version = scenario == "version" ? 1 : 2,
        };
        _directory.Setup(item => item.Exists(_recoveryDirectory)).Returns(true);
        _directory.Setup(item => item.EnumerateDirectories(_recoveryDirectory)).Returns([directory]);
        _file.Setup(item => item.Exists(manifestPath)).Returns(scenario == "manifest");
        _file.Setup(item => item.Exists(ownerPath)).Returns(scenario != "missing");
        _file.Setup(item => item.ReadAllTextAsync(ownerPath, TestContext.Current.CancellationToken))
            .Returns(() => scenario switch
            {
                "null" => Task.FromResult("null"),
                "json" => Task.FromResult("{"),
                "io" => Task.FromException<string>(new IOException()),
                "access" => Task.FromException<string>(new UnauthorizedAccessException()),
                _ => Task.FromResult(JsonSerializer.Serialize(owner, new JsonSerializerOptions(JsonSerializerDefaults.Web))),
            });

        var result = await _target.GetOrphanedCommitOwnersAsync(TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("version")]
    [InlineData("commit")]
    [InlineData("loaded")]
    [InlineData("root")]
    [InlineData("json")]
    [InlineData("io")]
    [InlineData("access")]
    public async Task GIVEN_InvalidOrUnreadableOrphanOwner_WHEN_ReadingStatuses_THEN_ShouldReturnConflict(string scenario)
    {
        var directory = _recoveryDirectory + "/CommitId";
        var ownerPath = directory + "/owner.json";
        var owner = new WorkspaceCommitOwner
        {
            CommitId = scenario == "commit" ? "OtherCommitId" : "CommitId",
            LoadedPath = scenario == "loaded" ? "Workspace.sln" : "/Workspace/Workspace.sln",
            WorkspaceRoot = scenario == "root" ? "Workspace" : "/Workspace",
            Version = scenario == "version" ? 1 : 2,
        };
        _directory.Setup(item => item.Exists(_recoveryDirectory)).Returns(true);
        _directory.Setup(item => item.EnumerateDirectories(_recoveryDirectory)).Returns([directory]);
        _directory.Setup(item => item.EnumerateFiles(_recoveryDirectory, "*.json", SearchOption.TopDirectoryOnly)).Returns([]);
        _file.Setup(item => item.Exists(directory + "/manifest.json")).Returns(false);
        _file.Setup(item => item.Exists(ownerPath)).Returns(true);
        _file.Setup(item => item.ReadAllTextAsync(ownerPath, TestContext.Current.CancellationToken))
            .Returns(() => scenario switch
            {
                "null" => Task.FromResult("null"),
                "json" => Task.FromResult("{"),
                "io" => Task.FromException<string>(new IOException()),
                "access" => Task.FromException<string>(new UnauthorizedAccessException()),
                _ => Task.FromResult(JsonSerializer.Serialize(owner, new JsonSerializerOptions(JsonSerializerDefaults.Web))),
            });

        var result = await _target.GetStatusesAsync(TestContext.Current.CancellationToken);

        var status = result.Should().ContainSingle().Which;
        status.CommitId.Should().Be("CommitId");
        status.State.Should().Be(RecoveryState.RecoveryConflict);
        status.Message.Should().Be("The recovery owner record is malformed or unreadable.");
        status.SolutionPath.Should().Be(scenario is "version" or "commit" or "root" ? "/Workspace/Workspace.sln" : string.Empty);
        status.WorkspaceRoot.Should().Be(scenario is "version" or "commit" or "loaded" ? "/Workspace" : string.Empty);
    }

    [Fact]
    public async Task GIVEN_MissingRecoveryDirectory_WHEN_ReadingOwners_THEN_ShouldReturnEmptyCollection()
    {
        var result = await _target.GetOrphanedCommitOwnersAsync(TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_ArtifactWithoutParentDirectory_WHEN_Persisting_THEN_ShouldRejectPlan()
    {
        var plan = new WorkspaceCommitPlan(CreateManifest(), new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["File.bin"] = new byte[] { 1 },
        });
        _path.Setup(item => item.GetDirectoryName(_recoveryDirectory + "/CommitId/File.bin")).Returns((string?)null);

        var action = async () => await _target.PersistPlanAsync(plan, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void GIVEN_CommitDirectoryAndLegacyStatus_WHEN_Deleting_THEN_ShouldDeleteBoth()
    {
        var directory = _recoveryDirectory + "/CommitId";
        var legacy = _recoveryDirectory + "/CommitId.json";
        _directory.Setup(item => item.Exists(directory)).Returns(true);
        _file.Setup(item => item.Exists(legacy)).Returns(true);

        _target.DeleteStatus("CommitId");

        _directory.Verify(item => item.Delete(directory, true), Times.Once);
        _file.Verify(item => item.Delete(legacy), Times.Once);
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

    private static WorkspaceCommitManifest CreateInvalidManifest(string scenario)
    {
        var manifest = CreateManifest();
        return scenario switch
        {
            "version" => manifest with { Version = 1 },
            "commit" => manifest with { CommitId = "OtherCommitId" },
            "loaded" => manifest with { LoadedPath = "Workspace.sln" },
            "root" => manifest with { WorkspaceRoot = "Workspace" },
            "outside" => manifest with { LoadedPath = "/Other/Workspace.sln" },
            "target" => manifest with { Entries = [CreateEntry("File.cs")], },
            "duplicate" => manifest with { Entries = [CreateEntry("/Workspace/File.cs"), CreateEntry("/Workspace/File.cs")], },
            "delete" => manifest with { Entries = [CreateEntry("/Workspace/File.cs") with { DeleteMarkerPath = "/Other/File.cs" }], },
            "backup" => manifest with { Entries = [CreateEntry("/Workspace/File.cs") with { BackupPath = "../File.bin" }], },
            "backupEmpty" => manifest with { Entries = [CreateEntry("/Workspace/File.cs") with { BackupPath = string.Empty }], },
            "staged" => manifest with { Entries = [CreateEntry("/Workspace/File.cs") with { StagedPath = "../File.bin" }], },
            "artifactArgument" => manifest with { Entries = [CreateEntry("/Workspace/File.cs") with { StagedPath = "invalid" }], },
            "artifactRelative" => manifest with { Entries = [CreateEntry("/Workspace/File.cs") with { StagedPath = "relative" }], },
            "invalidCommit" => manifest with { CommitId = "Invalid*CommitId" },
            "emptyCommit" => manifest with { CommitId = string.Empty },
            "created" => manifest with { CreatedDirectories = ["/Other/Directory"] },
            "createdRelative" => manifest with { CreatedDirectories = ["Directory"] },
            _ => throw new InvalidOperationException("Unknown scenario."),
        };
    }

    private static WorkspaceCommitManifest CreateManifest()
    {
        return new WorkspaceCommitManifest
        {
            CommitId = "CommitId",
            LoadedPath = "/Workspace/Workspace.sln",
            WorkspaceRoot = "/Workspace",
            State = RecoveryState.Prepared,
            Entries = [CreateEntry("/Workspace/File.cs")],
            CreatedDirectories = ["/Workspace/NewDirectory"],
        };
    }

    private static WorkspaceCommitEntry CreateEntry(string targetPath)
    {
        return new WorkspaceCommitEntry
        {
            TargetPath = targetPath,
            Operation = WorkspaceFileOperation.Replace,
            OriginalExists = true,
        };
    }
}
#pragma warning restore CA1869
