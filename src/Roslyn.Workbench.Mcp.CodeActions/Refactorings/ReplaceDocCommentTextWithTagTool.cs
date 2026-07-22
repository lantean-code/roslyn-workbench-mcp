using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ReplaceDocCommentTextWithTagTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ReplaceDocCommentTextWithTag.CSharpReplaceDocCommentTextWithTagCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public ReplaceDocCommentTextWithTagTool(ICodeActionSelectionStager selectionStager)
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
            titleStartsWith: "Use <");
    }
}
