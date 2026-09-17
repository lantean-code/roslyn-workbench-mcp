namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Projects active transaction revisions into operator-facing mutation provenance.
/// </summary>
internal static class TransactionMutationProvenanceFactory
{
    /// <summary>
    /// Creates provenance entries for revisions active at the transaction's current history position.
    /// </summary>
    /// <param name="transaction">The transaction whose active revisions are projected.</param>
    /// <returns>The active mutation provenance in revision order.</returns>
    public static TransactionMutationProvenance[] Create(WorkspaceTransaction transaction)
    {
        return transaction.Revisions
            .Take(transaction.CurrentRevision)
            .Select(static (revision, index) => new TransactionMutationProvenance
            {
                Revision = index + 1,
                Operation = revision.Operation,
                Summary = revision.Summary,
                CodeAction = revision.CodeActionProvenance,
            })
            .ToArray();
    }
}
