namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Compares compiler errors in a transaction baseline and staged solution.
/// </summary>
internal interface ITransactionCompilerValidationService
{
    /// <summary>
    /// Validates that the current transaction introduces no compiler errors in affected loaded project evaluations.
    /// </summary>
    /// <param name="session">The immutable Workspace session containing the active transaction.</param>
    /// <param name="cancellationToken">The token used to cancel validation.</param>
    /// <returns>The snapshot-bound compiler-impact result.</returns>
    ValueTask<TransactionCompilerValidationOutcome> ValidateAsync(
        WorkspaceSessionSnapshot session,
        CancellationToken cancellationToken);
}
