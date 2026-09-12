using System.Text.Json;
using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Recovery;

public sealed class RecoveryFormatWriterTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_RuntimeManifest_WHEN_Serializing_THEN_ShouldWriteCurrentPersistedFormat()
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
        var manifest = new WorkspaceCommitManifest
        {
            CommitId = "CommitId",
            LoadedPath = "LoadedPath",
            WorkspaceRoot = "WorkspaceRoot",
            State = RecoveryState.Applying,
            Entries = [entry],
            CreatedDirectories = ["CreatedDirectory"],
            Message = "Message",
        };

        var json = RecoveryFormatWriter.SerializeManifest(manifest, _serializerOptions);

        var header = RecoveryFormatReader.ReadHeader(json);
        var roundTrip = RecoveryFormatReader.ReadManifest(json, RecoveryFormatVersions.Current, _serializerOptions);
        header.Should().NotBeNull();
        header.Version.Should().Be(RecoveryFormatVersions.Current);
        roundTrip.Should().BeEquivalentTo(manifest);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_RuntimeOwner_WHEN_Serializing_THEN_ShouldWriteCurrentPersistedFormat()
    {
        var owner = new WorkspaceCommitOwner
        {
            CommitId = "CommitId",
            LoadedPath = "LoadedPath",
            WorkspaceRoot = "WorkspaceRoot",
        };

        var json = RecoveryFormatWriter.SerializeOwner(owner, _serializerOptions);

        var header = RecoveryFormatReader.ReadHeader(json);
        var roundTrip = RecoveryFormatReader.ReadOwner(json, RecoveryFormatVersions.Current, _serializerOptions);
        header.Should().NotBeNull();
        header.Version.Should().Be(RecoveryFormatVersions.Current);
        roundTrip.Should().BeEquivalentTo(owner);
    }
}
