namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the state of durable commit recovery.
/// </summary>
public sealed record RecoveryStatus
{
    /// <summary>
    /// Gets a value indicating whether the workspace identity could not be parsed.
    /// </summary>
    internal bool HasMalformedWorkspaceIdentity { get; init; }

    /// <summary>
    /// Absolute workspace root associated with the recovery record, or empty when malformed recovery evidence does not provide a safe path.
    /// </summary>
    internal string WorkspaceRoot { get; init; } = string.Empty;

    /// <summary>
    /// Durable commit identifier associated with the recovery record.
    /// </summary>
    [Description("Durable commit identifier associated with the recovery record.")]
    public required string CommitId { get; init; }

    /// <summary>
    /// Absolute solution or project path associated with the recovery record, or empty when malformed recovery evidence does not provide a safe path.
    /// </summary>
    [Description("Absolute solution or project path associated with the recovery record, or empty when malformed recovery evidence does not provide a safe path.")]
    public string SolutionPath { get; init; } = string.Empty;

    /// <summary>
    /// Current durable commit recovery state.
    /// </summary>
    [Description("Current durable commit recovery state.")]
    public RecoveryState State { get; init; }

    /// <summary>
    /// Human-readable recovery status or failure detail, when available.
    /// </summary>
    [Description("Human-readable recovery status or failure detail, when available.")]
    public string? Message { get; init; }
}
