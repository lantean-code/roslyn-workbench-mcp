using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IMutationStagingService
{
    ValueTask<WorkspaceOperationResult<MutationStagingOutcome>> StageAsync(
        string operationName,
        MutationProposal proposal,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken);
}
