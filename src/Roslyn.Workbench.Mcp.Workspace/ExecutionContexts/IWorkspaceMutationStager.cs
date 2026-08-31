namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

/// <summary>
/// Stages a mutation candidate into the active workspace transaction.
/// </summary>
internal interface IWorkspaceMutationStager
{
    /// <summary>
    /// Stages a candidate together with its diagnostics and warnings.
    /// </summary>
    /// <param name="operationName">The mutation operation name used for transaction history.</param>
    /// <param name="candidate">The candidate solution and changed documents.</param>
    /// <param name="diagnostics">Diagnostics produced while preparing the candidate.</param>
    /// <param name="warnings">Warnings produced while preparing the candidate.</param>
    /// <param name="cancellationToken">The token used to cancel staging.</param>
    /// <returns>The staging outcome.</returns>
    ValueTask<WorkspaceOperationResult<MutationStagingOutcome>> StageAsync(
        string operationName,
        WorkspaceMutationCandidate candidate,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken);
}
