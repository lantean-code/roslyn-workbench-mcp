namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceMutationStager : IWorkspaceMutationStager
{
    private readonly IMutationStagingService _mutationStagingService;

    public WorkspaceMutationStager(IMutationStagingService mutationStagingService)
    {
        _mutationStagingService = mutationStagingService;
    }

    public ValueTask<WorkspaceOperationResult<MutationStagingOutcome>> StageAsync(
        string operationName,
        WorkspaceMutationCandidate candidate,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        return _mutationStagingService.StageAsync(
            operationName,
            candidate,
            diagnostics,
            warnings,
            cancellationToken);
    }
}
