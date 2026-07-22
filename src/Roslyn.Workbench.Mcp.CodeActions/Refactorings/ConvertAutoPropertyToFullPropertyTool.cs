using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertAutoPropertyToFullPropertyTool : CodeActionMutationToolHandler<ConvertAutoPropertyToFullPropertyRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public ConvertAutoPropertyToFullPropertyTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(ConvertAutoPropertyToFullPropertyRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _selectionStager.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            ProviderId,
            title: "Convert to full property");
    }
}
