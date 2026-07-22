using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ExtractMethodTool : CodeActionMutationToolHandler<ExtractMethodRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider";
    private const string MethodTitle = "Extract method";
    private const string MethodEquivalenceKey = "Extract_method";
    private const string LocalFunctionTitle = "Extract local function";
    private const string LocalFunctionEquivalenceKey = "Extract_local_function";

    private readonly ICodeActionSelectionStager _selectionStager;

    public ExtractMethodTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(ExtractMethodRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (request.Selection is null)
        {
            var rejection = CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>(
                "InvalidRequest",
                "A location selector is required.");

            return ValueTask.FromResult(rejection);
        }

        var (title, equivalenceKey) = request.TargetKind switch
        {
            ExtractMethodTargetKind.LocalFunction => (LocalFunctionTitle, LocalFunctionEquivalenceKey),
            _ => (MethodTitle, MethodEquivalenceKey),
        };

        return _selectionStager.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = request.Selection,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = ProviderId,
            Title = title,
            EquivalenceKey = equivalenceKey,
        }, context, cancellationToken);
    }
}
