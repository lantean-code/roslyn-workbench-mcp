namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Creates change summaries and document diffs for workspace transactions.
/// </summary>
internal sealed class WorkspaceDiffService : IWorkspaceDiffBuilder
{
    /// <inheritdoc/>
    public async ValueTask<ChangeSummary> CreateChangeSummaryAsync(
        Solution baselineSolution,
        Solution currentSolution,
        IWorkspaceResolver resolver,
        CancellationToken cancellationToken)
    {
        return await WorkspaceDiffBuilder.CreateChangeSummaryAsync(
            baselineSolution,
            currentSolution,
            resolver,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<DocumentDiff?> CreateDocumentDiffAsync(
        Solution baselineSolution,
        Solution currentSolution,
        DocumentReference documentReference,
        IWorkspaceResolver resolver,
        int contextLines,
        CancellationToken cancellationToken)
    {
        return await WorkspaceDiffBuilder.CreateDocumentDiffAsync(
            baselineSolution,
            currentSolution,
            documentReference,
            resolver,
            contextLines,
            cancellationToken);
    }
}
