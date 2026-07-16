using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class IntroduceParameterTool : CodeActionMutationToolHandler<IntroduceParameterRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.IntroduceParameter.CSharpIntroduceParameterCodeRefactoringProvider";
    private const string UpdateCallSitesDirectlyTitle = "and update call sites directly";
    private const string IntoExtractedMethodTitle = "into extracted method to invoke at call sites";
    private const string IntoNewOverloadTitle = "into new overload";

    private readonly ICodeActionReplayService _replayService;

    public IntroduceParameterTool(ICodeActionReplayService replayService)
    {
        _replayService = replayService;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(IntroduceParameterRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (request.Selection is null)
        {
            return ValueTask.FromResult(CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("InvalidRequest", "A location selector is required."));
        }

        var title = request.Strategy switch
        {
            IntroduceParameterStrategy.IntoExtractedMethod => IntoExtractedMethodTitle,
            IntroduceParameterStrategy.IntoNewOverload => IntoNewOverloadTitle,
            _ => UpdateCallSitesDirectlyTitle,
        };

        IReadOnlyList<int> actionPath = request.Strategy switch
        {
            IntroduceParameterStrategy.IntoExtractedMethod => request.AllOccurrences ? [1, 1] : [0, 1],
            IntroduceParameterStrategy.IntoNewOverload => request.AllOccurrences ? [1, 2] : [0, 2],
            _ => request.AllOccurrences ? [1, 0] : [0, 0],
        };

        return _replayService.StageReplayCodeActionAsync(new ReplayCodeActionRequest
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
