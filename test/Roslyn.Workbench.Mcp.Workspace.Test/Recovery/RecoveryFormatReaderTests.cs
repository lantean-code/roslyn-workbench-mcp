using System.Text.Json;
using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Recovery;

public sealed class RecoveryFormatReaderTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void GIVEN_ValidCommonHeader_WHEN_ReadingHeader_THEN_ShouldReturnVersionAndWorkspaceIdentity()
    {
        const string json = """
            {
              "version": 1,
              "loadedPath": "LoadedPath",
              "workspaceRoot": "WorkspaceRoot"
            }
            """;

        var result = RecoveryFormatReader.ReadHeader(json);

        result.Should().NotBeNull();
        result.Version.Should().Be(RecoveryFormatVersions.V1);
        result.LoadedPath.Should().Be("LoadedPath");
        result.WorkspaceRoot.Should().Be("WorkspaceRoot");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"version\":\"1\"}")]
    [InlineData("{\"version\":2147483648}")]
    public void GIVEN_MalformedCommonHeader_WHEN_ReadingHeader_THEN_ShouldReturnNull(string json)
    {
        var result = RecoveryFormatReader.ReadHeader(json);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"loadedPath\":1,\"workspaceRoot\":false}")]
    public void GIVEN_MissingOrNonStringWorkspaceIdentity_WHEN_ReadingHeader_THEN_ShouldReturnNullIdentityFields(string json)
    {
        var result = RecoveryFormatReader.ReadHeader(json);

        result.Should().NotBeNull();
        result.LoadedPath.Should().BeNull();
        result.WorkspaceRoot.Should().BeNull();
    }

    [Fact]
    public void GIVEN_V1Manifest_WHEN_ReadingManifest_THEN_ShouldReturnNormalisedManifest()
    {
        var manifest = CreateRuntimeManifest();
        var json = RecoveryFormatWriter.SerializeManifest(manifest, _serializerOptions);

        var result = RecoveryFormatReader.ReadManifest(json, RecoveryFormatVersions.V1, _serializerOptions);

        result.Should().BeEquivalentTo(manifest);
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("commitId")]
    [InlineData("loadedPath")]
    [InlineData("workspaceRoot")]
    [InlineData("entries")]
    [InlineData("createdDirectories")]
    [InlineData("entry")]
    [InlineData("targetPath")]
    [InlineData("createdDirectory")]
    public void GIVEN_StructurallyMalformedV1Manifest_WHEN_ReadingManifest_THEN_ShouldReturnNull(string scenario)
    {
        var persisted = CreatePersistedManifest();
        var missingTargetEntry = new RecoveryEntryV1();
        persisted = scenario switch
        {
            "commitId" => persisted with { CommitId = null },
            "loadedPath" => persisted with { LoadedPath = null },
            "workspaceRoot" => persisted with { WorkspaceRoot = null },
            "entries" => persisted with { Entries = null },
            "createdDirectories" => persisted with { CreatedDirectories = null },
            "entry" => persisted with { Entries = [null] },
            "targetPath" => persisted with { Entries = [missingTargetEntry] },
            "createdDirectory" => persisted with { CreatedDirectories = [null] },
            _ => persisted,
        };
        var json = scenario == "payload"
            ? "null"
            : JsonSerializer.Serialize(persisted, _serializerOptions);

        var result = RecoveryFormatReader.ReadManifest(json, RecoveryFormatVersions.V1, _serializerOptions);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_V1Owner_WHEN_ReadingOwner_THEN_ShouldReturnNormalisedOwner()
    {
        var owner = new WorkspaceCommitOwner
        {
            CommitId = "CommitId",
            LoadedPath = "LoadedPath",
            WorkspaceRoot = "WorkspaceRoot",
        };
        var json = JsonSerializer.Serialize(owner, _serializerOptions);

        var result = RecoveryFormatReader.ReadOwner(json, RecoveryFormatVersions.V1, _serializerOptions);

        result.Should().BeEquivalentTo(owner);
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("commitId")]
    [InlineData("loadedPath")]
    [InlineData("workspaceRoot")]
    public void GIVEN_StructurallyMalformedV1Owner_WHEN_ReadingOwner_THEN_ShouldReturnNull(string scenario)
    {
        var persisted = new RecoveryOwnerV1
        {
            CommitId = scenario == "commitId" ? null : "CommitId",
            LoadedPath = scenario == "loadedPath" ? null : "LoadedPath",
            WorkspaceRoot = scenario == "workspaceRoot" ? null : "WorkspaceRoot",
        };
        var json = scenario == "payload"
            ? "null"
            : JsonSerializer.Serialize(persisted, _serializerOptions);

        var result = RecoveryFormatReader.ReadOwner(json, RecoveryFormatVersions.V1, _serializerOptions);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_UnsupportedVersion_WHEN_ReadingManifest_THEN_ShouldRejectInternalInvariantViolation()
    {
        const string json = "{}";

        Action action = () => RecoveryFormatReader.ReadManifest(json, RecoveryFormatVersions.Current + 1, _serializerOptions);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Recovery manifest format version 2 is not supported by this reader.");
    }

    [Fact]
    public void GIVEN_UnsupportedVersion_WHEN_ReadingOwner_THEN_ShouldRejectInternalInvariantViolation()
    {
        const string json = "{}";

        Action action = () => RecoveryFormatReader.ReadOwner(json, RecoveryFormatVersions.Current + 1, _serializerOptions);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Recovery owner format version 2 is not supported by this reader.");
    }

    private static RecoveryManifestV1 CreatePersistedManifest()
    {
        return new RecoveryManifestV1
        {
            CommitId = "CommitId",
            LoadedPath = "LoadedPath",
            WorkspaceRoot = "WorkspaceRoot",
            State = RecoveryState.Prepared,
            Entries = [],
            CreatedDirectories = [],
        };
    }

    private static WorkspaceCommitManifest CreateRuntimeManifest()
    {
        var entry = new WorkspaceCommitEntry
        {
            TargetPath = "TargetPath",
            Operation = WorkspaceFileOperation.Replace,
            OriginalExists = true,
            OriginalHash = "OriginalHash",
            IntendedHash = "IntendedHash",
            OriginalUnixFileMode = UnixFileMode.UserRead,
            IntendedUnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            BackupPath = "BackupPath",
            StagedPath = "StagedPath",
            DeleteMarkerPath = "DeleteMarkerPath",
        };

        return new WorkspaceCommitManifest
        {
            CommitId = "CommitId",
            LoadedPath = "LoadedPath",
            WorkspaceRoot = "WorkspaceRoot",
            State = RecoveryState.Applying,
            Entries = [entry],
            CreatedDirectories = ["CreatedDirectory"],
            Message = "Message",
        };
    }
}
