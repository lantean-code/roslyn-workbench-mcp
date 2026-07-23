using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertIfToSwitchTool : CodeActionMutationToolHandler<ConvertIfToSwitchRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.ConvertIfToSwitch.CSharpConvertIfToSwitchCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public ConvertIfToSwitchTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(ConvertIfToSwitchRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var title = request.Kind == ConvertIfToSwitchKind.Expression
            ? "Convert to 'switch' expression"
            : "Convert to 'switch' statement";

        return _selectionStager.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            _providerId,
            title: title);
    }
}
