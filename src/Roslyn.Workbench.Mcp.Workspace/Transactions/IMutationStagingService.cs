using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IMutationStagingService
{
    ValueTask<WorkspaceOperationResult<MutationStagingOutcome>> StageAsync(
        string operationName,
        WorkspaceMutationCandidate candidate,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken);
}
