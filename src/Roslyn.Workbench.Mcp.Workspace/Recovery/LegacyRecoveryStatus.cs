namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Represents a recovery status persisted by the legacy flat-file format.
/// </summary>
internal sealed record LegacyRecoveryStatus
{
    /// <summary>
    /// Gets the loaded solution or project path associated with the commit.
    /// </summary>
    public string SolutionPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the workspace root associated with the commit.
    /// </summary>
    public string WorkspaceRoot { get; init; } = string.Empty;

    /// <summary>
    /// Gets the recorded recovery state.
    /// </summary>
    public RecoveryState State { get; init; }

    /// <summary>
    /// Gets supplementary recovery information.
    /// </summary>
    public string? Message { get; init; }
}
