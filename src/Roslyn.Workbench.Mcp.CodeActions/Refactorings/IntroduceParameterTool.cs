using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class IntroduceParameterTool : CodeActionMutationToolHandler<IntroduceParameterRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.IntroduceParameter.CSharpIntroduceParameterCodeRefactoringProvider";
    private const string UpdateCallSitesDirectlyTitle = "and update call sites directly";
    private const string IntoExtractedMethodTitle = "into extracted method to invoke at call sites";
    private const string IntoNewOverloadTitle = "into new overload";

    private readonly ICodeActionSelectionStager _selectionStager;

    public IntroduceParameterTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(IntroduceParameterRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (request.Selection is null)
        {
            var rejection = CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>(
                "InvalidRequest",
                "A location selector is required.");

            return ValueTask.FromResult(rejection);
        }

        var title = request.Strategy switch
        {
            IntroduceParameterStrategy.IntoExtractedMethod => IntoExtractedMethodTitle,
            IntroduceParameterStrategy.IntoNewOverload => IntoNewOverloadTitle,
            _ => UpdateCallSitesDirectlyTitle,
        };

        var occurrenceIndex = request.AllOccurrences ? 1 : 0;
        IReadOnlyList<int> actionPath = request.Strategy switch
        {
            IntroduceParameterStrategy.IntoExtractedMethod => [occurrenceIndex, 1],
            IntroduceParameterStrategy.IntoNewOverload => [occurrenceIndex, 2],
            _ => [occurrenceIndex, 0],
        };

        return _selectionStager.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = request.Selection,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = ProviderId,
            Title = title,
            EquivalenceKey = title,
            ActionPath = actionPath,
        }, context, cancellationToken);
    }
}
