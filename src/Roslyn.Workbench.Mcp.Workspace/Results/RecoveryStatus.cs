namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the state of durable commit recovery.
/// </summary>
public sealed record RecoveryStatus
{
    internal string WorkspaceRoot { get; init; } = string.Empty;

    /// <summary>
    /// Gets the durable commit identifier.
    /// </summary>
    public string CommitId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the solution path associated with the recovery record.
    /// </summary>
    public string SolutionPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the recovery state.
    /// </summary>
    public RecoveryState State { get; init; }

    /// <summary>
    /// Gets the optional human-readable recovery message.
    /// </summary>
    public string? Message { get; init; }
}
