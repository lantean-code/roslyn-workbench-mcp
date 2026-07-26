using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ReplaceMethodWithPropertyTool : CodeActionMutationToolHandler<ReplaceMethodWithPropertyRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.ReplaceMethodWithProperty.ReplaceMethodWithPropertyCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public ReplaceMethodWithPropertyTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(
        ReplaceMethodWithPropertyRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        var actionPath = request.Kind == ReplaceMethodWithPropertyKind.GetterOnly
            ? new[] { 0 }
            : [1];

        return _selectionStager.StageSelectionAsync(
            request.Method,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            _providerId,
            actionPath: actionPath);
    }
}
