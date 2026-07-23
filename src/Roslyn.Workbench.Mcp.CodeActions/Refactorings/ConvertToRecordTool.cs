using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertToRecordTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.ConvertToRecord.CSharpConvertToRecordRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public ConvertToRecordTool(ICodeActionSelectionStager selectionStager)
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
            title: "Convert to positional record");
    }
}
