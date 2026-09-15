using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Represents either a completed transaction review projection or its planning failure.
/// </summary>
internal sealed class TransactionReviewBuildResult
{
    /// <summary>
    /// Gets the completed review outcome when construction succeeds.
    /// </summary>
    public TransactionReviewOutcome? Outcome { get; }

    /// <summary>
    /// Gets the failure message when no safe review could be produced.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets whether construction produced a review outcome.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Outcome))]
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsSucceeded => Outcome is not null;

    private TransactionReviewBuildResult(
        TransactionReviewOutcome? outcome,
        string? errorMessage)
    {
        Outcome = outcome;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a successful review construction result.
    /// </summary>
    /// <param name="outcome">The completed transaction review.</param>
    /// <returns>A successful result.</returns>
    public static TransactionReviewBuildResult Succeeded(TransactionReviewOutcome outcome)
    {
        return new TransactionReviewBuildResult(outcome, errorMessage: null);
    }

    /// <summary>
    /// Creates a failed review construction result.
    /// </summary>
    /// <param name="errorMessage">The reason no safe review could be produced.</param>
    /// <returns>A failed result.</returns>
    public static TransactionReviewBuildResult Failed(string errorMessage)
    {
        return new TransactionReviewBuildResult(outcome: null, errorMessage);
    }
}
