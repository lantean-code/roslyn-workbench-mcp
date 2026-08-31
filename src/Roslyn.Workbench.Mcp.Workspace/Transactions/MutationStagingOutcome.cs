namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Describes the transaction revision and preview produced by a staged mutation.
/// </summary>
internal sealed record MutationStagingOutcome
{
    /// <summary>
    /// Gets the operation name recorded in transaction history.
    /// </summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concise summary of the staged change.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets transaction state after staging.
    /// </summary>
    public TransactionInfo Transaction { get; init; } = new();

    /// <summary>
    /// Gets the compact preview of the staged mutation.
    /// </summary>
    public required MutationPreview Preview { get; init; }

    /// <summary>
    /// Gets the changed-document identities in the new revision.
    /// </summary>
    public ChangeSummary Changes { get; init; } = new();
}
