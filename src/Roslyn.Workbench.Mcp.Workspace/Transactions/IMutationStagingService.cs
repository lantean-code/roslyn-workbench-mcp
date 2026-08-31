namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Validates and appends a mutation candidate to the active transaction.
/// </summary>
internal interface IMutationStagingService
{
    /// <summary>
    /// Validates, normalises, and stages one mutation candidate.
    /// </summary>
    /// <param name="operationName">The operation name recorded in transaction history and results.</param>
    /// <param name="candidate">The proposed solution and its staging precondition.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the staged mutation result or a structured rejection.</returns>
    ValueTask<WorkspaceOperationResult<MutationStagingOutcome>> StageAsync(
        string operationName,
        WorkspaceMutationCandidate candidate,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken);
}
