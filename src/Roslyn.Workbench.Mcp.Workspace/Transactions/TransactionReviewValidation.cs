namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Reports one validation completed while producing a transaction review receipt.
/// </summary>
internal sealed record TransactionReviewValidation
{
    /// <summary>
    /// Gets the stable validation name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets whether the validation succeeded.
    /// </summary>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// Gets the concise validation result.
    /// </summary>
    public required string Message { get; init; }
}
