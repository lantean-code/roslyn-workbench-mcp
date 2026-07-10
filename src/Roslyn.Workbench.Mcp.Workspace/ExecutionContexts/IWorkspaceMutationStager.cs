namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal interface IWorkspaceMutationStager
{
    ValueTask<WorkspaceOperationResult<MutationStagingOutcome>> StageAsync(
        string operationName,
        WorkspaceMutationProposal proposal,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken);
}
