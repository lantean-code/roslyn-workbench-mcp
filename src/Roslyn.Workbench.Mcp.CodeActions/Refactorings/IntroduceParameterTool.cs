using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class IntroduceParameterTool : CodeActionMutationToolHandler<IntroduceParameterRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.IntroduceParameter.CSharpIntroduceParameterCodeRefactoringProvider";
    private const string _updateCallSitesDirectlyTitle = "and update call sites directly";
    private const string _intoExtractedMethodTitle = "into extracted method to invoke at call sites";
    private const string _intoNewOverloadTitle = "into new overload";

    private readonly ICodeActionSelectionStager _selectionStager;

    public IntroduceParameterTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(IntroduceParameterRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var title = request.Strategy switch
        {
            IntroduceParameterStrategy.IntoExtractedMethod => _intoExtractedMethodTitle,
            IntroduceParameterStrategy.IntoNewOverload => _intoNewOverloadTitle,
            _ => _updateCallSitesDirectlyTitle,
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
            ProviderId = _providerId,
            Title = title,
            EquivalenceKey = title,
            ActionPath = actionPath,
        }, context, cancellationToken);
    }
}
