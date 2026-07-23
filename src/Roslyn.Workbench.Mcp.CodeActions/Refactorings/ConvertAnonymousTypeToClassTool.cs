using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertAnonymousTypeToClassTool : CodeActionMutationToolHandler<ConvertAnonymousTypeToClassRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public ConvertAnonymousTypeToClassTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(ConvertAnonymousTypeToClassRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var title = request.Kind == ConvertAnonymousTypeToClassKind.Record
            ? "Convert to record"
            : "Convert to class";

        return _selectionStager.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            _providerId,
            title: title);
    }
}
