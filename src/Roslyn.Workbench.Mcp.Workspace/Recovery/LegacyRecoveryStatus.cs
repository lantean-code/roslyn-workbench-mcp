namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal sealed record LegacyRecoveryStatus
{
    public string SolutionPath { get; init; } = string.Empty;

    public string WorkspaceRoot { get; init; } = string.Empty;

    public RecoveryState State { get; init; }

    public string? Message { get; init; }
}
