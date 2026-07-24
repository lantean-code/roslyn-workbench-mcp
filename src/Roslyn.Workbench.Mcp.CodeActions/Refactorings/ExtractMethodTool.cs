using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ExtractMethodTool : CodeActionMutationToolHandler<ExtractMethodRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider";
    private const string _methodTitle = "Extract method";
    private const string _methodEquivalenceKey = "Extract_method";
    private const string _localFunctionTitle = "Extract local function";
    private const string _localFunctionEquivalenceKey = "Extract_local_function";

    private readonly ICodeActionSelectionStager _selectionStager;

    public ExtractMethodTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(ExtractMethodRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var (title, equivalenceKey) = request.TargetKind switch
        {
            ExtractMethodTargetKind.LocalFunction => (_localFunctionTitle, _localFunctionEquivalenceKey),
            _ => (_methodTitle, _methodEquivalenceKey),
        };

        return _selectionStager.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = request.Selection,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = _providerId,
            Title = title,
            EquivalenceKey = equivalenceKey,
        }, context, cancellationToken);
    }
}
