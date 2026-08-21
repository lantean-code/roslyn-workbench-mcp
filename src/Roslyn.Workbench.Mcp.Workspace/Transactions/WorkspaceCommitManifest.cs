using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceCommitManifest
{
    [JsonIgnore]
    public bool HasMalformedWorkspaceIdentity { get; init; }

    public int Version { get; init; } = 1;

    public required string CommitId { get; init; }

    public required string LoadedPath { get; init; }

    public required string WorkspaceRoot { get; init; }

    public required RecoveryState State { get; init; }

    public required IReadOnlyList<WorkspaceCommitEntry> Entries { get; init; }

    public required IReadOnlyList<string> CreatedDirectories { get; init; }

    public string? Message { get; init; }
}
