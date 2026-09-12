using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Maps runtime recovery state to the persisted shape written by the current Host.
/// </summary>
internal static class RecoveryFormatWriter
{
    /// <summary>
    /// Serializes a recovery manifest using the current persisted format.
    /// </summary>
    /// <param name="manifest">The runtime recovery manifest.</param>
    /// <param name="serializerOptions">The recovery JSON serializer options.</param>
    /// <returns>The serialized current-format manifest.</returns>
    public static string SerializeManifest(WorkspaceCommitManifest manifest, JsonSerializerOptions serializerOptions)
    {
        return JsonSerializer.Serialize(CreateV1Manifest(manifest), serializerOptions);
    }

    /// <summary>
    /// Serializes a recovery owner record using the current persisted format.
    /// </summary>
    /// <param name="owner">The runtime recovery owner record.</param>
    /// <param name="serializerOptions">The recovery JSON serializer options.</param>
    /// <returns>The serialized current-format owner record.</returns>
    public static string SerializeOwner(WorkspaceCommitOwner owner, JsonSerializerOptions serializerOptions)
    {
        return JsonSerializer.Serialize(CreateV1Owner(owner), serializerOptions);
    }

    private static RecoveryManifestV1 CreateV1Manifest(WorkspaceCommitManifest manifest)
    {
        return new RecoveryManifestV1
        {
            CommitId = manifest.CommitId,
            LoadedPath = manifest.LoadedPath,
            WorkspaceRoot = manifest.WorkspaceRoot,
            State = manifest.State,
            Entries = manifest.Entries.Select(CreateV1Entry).ToArray(),
            CreatedDirectories = manifest.CreatedDirectories.ToArray(),
            Message = manifest.Message,
        };
    }

    private static RecoveryEntryV1 CreateV1Entry(WorkspaceCommitEntry entry)
    {
        return new RecoveryEntryV1
        {
            TargetPath = entry.TargetPath,
            Operation = entry.Operation,
            OriginalExists = entry.OriginalExists,
            OriginalHash = entry.OriginalHash,
            IntendedHash = entry.IntendedHash,
            OriginalUnixFileMode = entry.OriginalUnixFileMode,
            IntendedUnixFileMode = entry.IntendedUnixFileMode,
            BackupPath = entry.BackupPath,
            StagedPath = entry.StagedPath,
            DeleteMarkerPath = entry.DeleteMarkerPath,
        };
    }

    private static RecoveryOwnerV1 CreateV1Owner(WorkspaceCommitOwner owner)
    {
        return new RecoveryOwnerV1
        {
            CommitId = owner.CommitId,
            LoadedPath = owner.LoadedPath,
            WorkspaceRoot = owner.WorkspaceRoot,
        };
    }
}
