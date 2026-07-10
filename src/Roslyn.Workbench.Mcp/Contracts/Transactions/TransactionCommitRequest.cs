using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents a request to commit the active transaction.
/// </summary>
public sealed record TransactionCommitRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the expected snapshot precondition.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
