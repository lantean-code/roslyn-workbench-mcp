namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

/// <summary>
/// Stages a previously discovered or prepared Code Action in the active transaction.
/// </summary>
internal sealed class StageCodeActionTool : CodeActionMutationToolHandler<StageCodeActionRequest>
{
    private readonly ICodeActionStager _stager;

    /// <summary>
    /// Initializes a new instance of the <see cref="StageCodeActionTool"/> class.
    /// </summary>
    /// <param name="stager">The component that resolves a Code Action into a candidate solution.</param>
    public StageCodeActionTool(ICodeActionStager stager)
    {
        _stager = stager;
    }

    /// <inheritdoc/>
    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(StageCodeActionRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _stager.StageAsync(request, context, cancellationToken);
    }
}
