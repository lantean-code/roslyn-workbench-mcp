using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ReverseForStatementTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ReverseForStatement.CSharpReverseForStatementCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public ReverseForStatementTool(ICodeActionSelectionStager selectionStager)
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
            ProviderId,
            title: "Reverse 'for' statement");
    }
}
