namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the state of durable commit recovery.
/// </summary>
public sealed record RecoveryStatus
{
    internal bool HasMalformedWorkspaceIdentity { get; init; }

    internal string WorkspaceRoot { get; init; } = string.Empty;

    /// <summary>
    /// Gets the durable commit identifier.
    /// </summary>
    [Description("Durable commit identifier associated with the recovery record.")]
    public required string CommitId { get; init; }

    /// <summary>
    /// Gets the solution path associated with the recovery record.
    /// </summary>
    [Description("Absolute solution or project path associated with the recovery record, or empty when malformed recovery evidence does not provide a safe path.")]
    public string SolutionPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the recovery state.
    /// </summary>
    [Description("Current durable commit recovery state.")]
    public RecoveryState State { get; init; }

    /// <summary>
    /// Gets the optional human-readable recovery message.
    /// </summary>
    [Description("Human-readable recovery status or failure detail, when available.")]
    public string? Message { get; init; }
}
