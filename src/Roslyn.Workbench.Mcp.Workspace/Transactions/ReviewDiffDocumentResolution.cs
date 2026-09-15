namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Represents either a resolved optional review-diff document or its Workspace rejection.
/// </summary>
internal sealed class ReviewDiffDocumentResolution
{
    /// <summary>
    /// Gets the selected document when a detailed diff was requested and resolved.
    /// </summary>
    public DocumentReference? Document { get; }

    /// <summary>
    /// Gets the Workspace rejection when document resolution failed.
    /// </summary>
    public WorkspaceOperationResult<TransactionReviewOutcome>? Error { get; }

    private ReviewDiffDocumentResolution(
        DocumentReference? document,
        WorkspaceOperationResult<TransactionReviewOutcome>? error)
    {
        Document = document;
        Error = error;
    }

    /// <summary>
    /// Creates a successful optional document resolution.
    /// </summary>
    /// <param name="document">The resolved document, or <see langword="null"/> when no diff was requested.</param>
    /// <returns>A successful resolution.</returns>
    public static ReviewDiffDocumentResolution Succeeded(DocumentReference? document)
    {
        return new ReviewDiffDocumentResolution(document, error: null);
    }

    /// <summary>
    /// Creates a failed document resolution.
    /// </summary>
    /// <param name="error">The Workspace rejection describing the failure.</param>
    /// <returns>A failed resolution.</returns>
    public static ReviewDiffDocumentResolution Failed(WorkspaceOperationResult<TransactionReviewOutcome> error)
    {
        return new ReviewDiffDocumentResolution(document: null, error);
    }
}
