namespace Roslyn.Workbench.Mcp.Transactions;

/// <summary>
/// Stores memory-only transaction commit confirmation for the current server process.
/// </summary>
internal interface ICommitConfirmationState
{
    /// <summary>
    /// Gets whether transaction commits are approved for the remainder of the server process.
    /// </summary>
    bool IsApprovedForSession { get; }

    /// <summary>
    /// Approves transaction commits for the remainder of the server process.
    /// </summary>
    void ApproveForSession();
}
