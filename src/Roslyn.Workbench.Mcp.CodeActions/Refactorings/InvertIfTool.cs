using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class InvertIfTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.InvertIf.CSharpInvertIfCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public InvertIfTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(LocationRefactoringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _selectionStager.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            _providerId,
            title: "Invert if");
    }
}
