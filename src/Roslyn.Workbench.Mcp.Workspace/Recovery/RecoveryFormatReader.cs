using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Probes recovery evidence and dispatches supported formats to their version-specific readers.
/// </summary>
internal static class RecoveryFormatReader
{
    /// <summary>
    /// Reads the common recovery header without interpreting format-specific state.
    /// </summary>
    /// <param name="json">The persisted recovery JSON.</param>
    /// <returns>The parsed header, or <see langword="null"/> when the common header is malformed.</returns>
    public static RecoveryEvidenceHeader? ReadHeader(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("version", out var versionElement)
            || versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out var version))
        {
            return null;
        }

        var loadedPath = ReadOptionalString(root, "loadedPath");
        var workspaceRoot = ReadOptionalString(root, "workspaceRoot");
        return new RecoveryEvidenceHeader(version, loadedPath, workspaceRoot);
    }

    /// <summary>
    /// Reads and normalises a supported recovery manifest.
    /// </summary>
    /// <param name="json">The persisted recovery JSON.</param>
    /// <param name="version">The format version obtained from the common header.</param>
    /// <param name="serializerOptions">The recovery JSON serializer options.</param>
    /// <returns>The normalised manifest, or <see langword="null"/> when the supported payload represents JSON null.</returns>
    public static WorkspaceCommitManifest? ReadManifest(string json, int version, JsonSerializerOptions serializerOptions)
    {
        return version switch
        {
            RecoveryFormatVersions.V1 => ReadV1Manifest(json, serializerOptions),
            _ => throw new InvalidOperationException($"Recovery manifest format version {version} is not supported by this reader."),
        };
    }

    /// <summary>
    /// Reads and normalises a supported recovery owner record.
    /// </summary>
    /// <param name="json">The persisted recovery JSON.</param>
    /// <param name="version">The format version obtained from the common header.</param>
    /// <param name="serializerOptions">The recovery JSON serializer options.</param>
    /// <returns>The normalised owner record, or <see langword="null"/> when the supported payload represents JSON null.</returns>
    public static WorkspaceCommitOwner? ReadOwner(string json, int version, JsonSerializerOptions serializerOptions)
    {
        return version switch
        {
            RecoveryFormatVersions.V1 => ReadV1Owner(json, serializerOptions),
            _ => throw new InvalidOperationException($"Recovery owner format version {version} is not supported by this reader."),
        };
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static WorkspaceCommitManifest? ReadV1Manifest(string json, JsonSerializerOptions serializerOptions)
    {
        var persisted = JsonSerializer.Deserialize<RecoveryManifestV1>(json, serializerOptions);
        if (persisted?.CommitId is null
            || persisted.LoadedPath is null
            || persisted.WorkspaceRoot is null
            || persisted.Entries is null
            || persisted.CreatedDirectories is null)
        {
            return null;
        }

        var entries = new List<WorkspaceCommitEntry>(persisted.Entries.Count);
        foreach (var persistedEntry in persisted.Entries)
        {
            var entry = CreateRuntimeEntry(persistedEntry);
            if (entry is null)
            {
                return null;
            }

            entries.Add(entry);
        }

        var createdDirectories = new List<string>(persisted.CreatedDirectories.Count);
        foreach (var persistedDirectory in persisted.CreatedDirectories)
        {
            if (persistedDirectory is null)
            {
                return null;
            }

            createdDirectories.Add(persistedDirectory);
        }

        return new WorkspaceCommitManifest
        {
            Version = RecoveryFormatVersions.V1,
            CommitId = persisted.CommitId,
            LoadedPath = persisted.LoadedPath,
            WorkspaceRoot = persisted.WorkspaceRoot,
            State = persisted.State,
            Entries = entries,
            CreatedDirectories = createdDirectories,
            Message = persisted.Message,
        };
    }

    private static WorkspaceCommitOwner? ReadV1Owner(string json, JsonSerializerOptions serializerOptions)
    {
        var persisted = JsonSerializer.Deserialize<RecoveryOwnerV1>(json, serializerOptions);
        if (persisted?.CommitId is null
            || persisted.LoadedPath is null
            || persisted.WorkspaceRoot is null)
        {
            return null;
        }

        return new WorkspaceCommitOwner
        {
            Version = RecoveryFormatVersions.V1,
            CommitId = persisted.CommitId,
            LoadedPath = persisted.LoadedPath,
            WorkspaceRoot = persisted.WorkspaceRoot,
        };
    }

    private static WorkspaceCommitEntry? CreateRuntimeEntry(RecoveryEntryV1? persisted)
    {
        if (persisted?.TargetPath is null)
        {
            return null;
        }

        return new WorkspaceCommitEntry
        {
            TargetPath = persisted.TargetPath,
            Operation = persisted.Operation,
            OriginalExists = persisted.OriginalExists,
            OriginalHash = persisted.OriginalHash,
            IntendedHash = persisted.IntendedHash,
            OriginalUnixFileMode = persisted.OriginalUnixFileMode,
            IntendedUnixFileMode = persisted.IntendedUnixFileMode,
            BackupPath = persisted.BackupPath,
            StagedPath = persisted.StagedPath,
            DeleteMarkerPath = persisted.DeleteMarkerPath,
        };
    }
}
