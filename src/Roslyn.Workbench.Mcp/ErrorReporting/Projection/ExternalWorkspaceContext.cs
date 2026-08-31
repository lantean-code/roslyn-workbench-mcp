namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

/// <summary>
/// Contains non-identifying workspace state that may help diagnose a failed operation.
/// </summary>
internal sealed record ExternalWorkspaceContext
{
    /// <summary>
    /// Gets the workspace lifecycle state at the time of failure.
    /// </summary>
    public required string LifecycleState { get; init; }

    /// <summary>
    /// Gets the workspace epoch active during the failure.
    /// </summary>
    public long WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the number of loaded projects at the time of failure.
    /// </summary>
    public int ProjectCount { get; init; }

    /// <summary>
    /// Gets the number of loaded documents at the time of failure.
    /// </summary>
    public int DocumentCount { get; init; }

    /// <summary>
    /// Gets the active transaction revision when the failure occurred in a transaction.
    /// </summary>
    public int? TransactionRevision { get; init; }
}
