using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class AddImportTool : CodeActionMutationToolHandler<AddImportRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public AddImportTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(AddImportRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var titleDoesNotContain = request.SimplifyAllOccurrences ? null : "simplify all occurrences";

        return _selectionStager.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            _providerId,
            titleStartsWith: "Add 'using ",
            titleDoesNotContain: titleDoesNotContain);
    }
}
