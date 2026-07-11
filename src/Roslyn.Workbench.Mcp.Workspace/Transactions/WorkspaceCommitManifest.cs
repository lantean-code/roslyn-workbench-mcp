using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceCommitManifest
{
    public int Version { get; init; } = 2;

    public required string CommitId { get; init; }

    public required string LoadedPath { get; init; }

    public required string WorkspaceRoot { get; init; }

    public required RecoveryState State { get; init; }

    public required IReadOnlyList<WorkspaceCommitEntry> Entries { get; init; }

    public required IReadOnlyList<string> CreatedDirectories { get; init; }

    public string? Message { get; init; }
}
