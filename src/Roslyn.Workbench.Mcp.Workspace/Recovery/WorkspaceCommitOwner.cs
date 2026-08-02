namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal sealed record WorkspaceCommitOwner
{
    public int Version { get; init; } = 1;

    public required string CommitId { get; init; }

    public required string LoadedPath { get; init; }

    public required string WorkspaceRoot { get; init; }
}
