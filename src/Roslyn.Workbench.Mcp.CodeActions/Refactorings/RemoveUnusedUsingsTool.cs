using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class RemoveUnusedUsingsTool : CodeActionMutationToolHandler<RemoveUnusedUsingsRequest>
{
    private const string FixableDiagnosticId = "RemoveUnnecessaryImportsFixable";

    private readonly ICodeActionScopedFixService _scopedFixService;

    public RemoveUnusedUsingsTool(ICodeActionScopedFixService scopedFixService)
    {
        _scopedFixService = scopedFixService;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(RemoveUnusedUsingsRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _scopedFixService.StageScopedCodeFixAsync(new ScopedCodeFixRequest
        {
            Scope = request.Scope,
            ExpectedSnapshot = request.ExpectedSnapshot,
            DiagnosticIds = [FixableDiagnosticId],
            Title = "Remove unnecessary usings",
            SyntheticDiagnosticId = FixableDiagnosticId,
        }, context, cancellationToken);
    }
}
