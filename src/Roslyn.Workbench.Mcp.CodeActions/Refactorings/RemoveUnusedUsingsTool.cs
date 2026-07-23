using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class RemoveUnusedUsingsTool : CodeActionMutationToolHandler<RemoveUnusedUsingsRequest>
{
    private const string _fixableDiagnosticId = "RemoveUnnecessaryImportsFixable";

    private readonly IScopedCodeFixStager _scopedFixStager;

    public RemoveUnusedUsingsTool(IScopedCodeFixStager scopedFixStager)
    {
        _scopedFixStager = scopedFixStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(RemoveUnusedUsingsRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _scopedFixStager.StageScopedCodeFixAsync(new ScopedCodeFixRequest
        {
            Scope = request.Scope,
            ExpectedSnapshot = request.ExpectedSnapshot,
            DiagnosticIds = [_fixableDiagnosticId],
            Title = "Remove unnecessary usings",
            SyntheticDiagnosticId = _fixableDiagnosticId,
        }, context, cancellationToken);
    }
}
