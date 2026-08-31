namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

/// <summary>
/// Adapts execution-context mutation staging to the workspace transaction staging service.
/// </summary>
internal sealed class WorkspaceMutationStager : IWorkspaceMutationStager
{
    private readonly IMutationStagingService _mutationStagingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceMutationStager"/> class.
    /// </summary>
    /// <param name="mutationStagingService">The transaction staging service.</param>
    public WorkspaceMutationStager(IMutationStagingService mutationStagingService)
    {
        _mutationStagingService = mutationStagingService;
    }

    /// <inheritdoc/>
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
