namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Builds bounded change summaries and unified document diffs between solution snapshots.
/// </summary>
internal interface IWorkspaceDiffBuilder
{
    /// <summary>
    /// Summarises document additions, removals, and modifications between two solutions.
    /// </summary>
    /// <param name="baselineSolution">The solution used as the comparison baseline.</param>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="resolver">The resolver used to obtain canonical workspace data.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the created change summary.</returns>
    ValueTask<ChangeSummary> CreateChangeSummaryAsync(
        Solution baselineSolution,
        Solution currentSolution,
        IWorkspaceResolver resolver,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds a unified diff for one changed document.
    /// </summary>
    /// <param name="baselineSolution">The solution used as the comparison baseline.</param>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="documentReference">The document identity represented by the generated diff.</param>
    /// <param name="resolver">The resolver used to obtain canonical workspace data.</param>
    /// <param name="contextLines">The number of unchanged context lines to include around each difference.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the created document diff.</returns>
    ValueTask<DocumentDiff?> CreateDocumentDiffAsync(
        Solution baselineSolution,
        Solution currentSolution,
        DocumentReference documentReference,
        IWorkspaceResolver resolver,
        int contextLines,
        CancellationToken cancellationToken);
}
