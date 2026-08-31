namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Describes the committed workspace and final transaction state.
/// </summary>
internal sealed record TransactionCommitOutcome
{
    /// <summary>
    /// Gets the workspace snapshot published after commit.
    /// </summary>
    public bool Committed { get; init; }

    /// <summary>
    /// Gets the completed transaction information.
    /// </summary>
    public TransactionInfo? Transaction { get; init; }
}
