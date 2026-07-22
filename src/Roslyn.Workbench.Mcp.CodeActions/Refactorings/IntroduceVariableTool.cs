using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class IntroduceVariableTool : CodeActionMutationToolHandler<IntroduceVariableRequest>
{
    private readonly ICodeActionSelectionStager _selectionStager;

    public IntroduceVariableTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(IntroduceVariableRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (request.Selection is null)
        {
            var rejection = CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>(
                "InvalidRequest",
                "A location selector is required.");

            return ValueTask.FromResult(rejection);
        }

        var replayRequest = request.Kind switch
        {
            IntroduceVariableKind.LocalAllOccurrences => CreateReplayRequest(request, "Introduce local for all occurrences of "),
            IntroduceVariableKind.LocalConstant => CreateReplayRequest(request, "Introduce local constant for ", "all occurrences"),
            IntroduceVariableKind.LocalConstantAllOccurrences => CreateReplayRequest(request, "Introduce local constant for all occurrences of "),
            IntroduceVariableKind.Constant => CreateReplayRequest(request, "Introduce constant for ", "all occurrences"),
            IntroduceVariableKind.ConstantAllOccurrences => CreateReplayRequest(request, "Introduce constant for all occurrences of "),
            IntroduceVariableKind.Field => CreateReplayRequest(request, "Introduce field for ", "all occurrences"),
            IntroduceVariableKind.FieldAllOccurrences => CreateReplayRequest(request, "Introduce field for all occurrences of "),
            IntroduceVariableKind.QueryVariable => CreateReplayRequest(request, "Introduce query variable for ", "all occurrences"),
            IntroduceVariableKind.QueryVariableAllOccurrences => CreateReplayRequest(request, "Introduce query variable for all occurrences of "),
            _ => CreateReplayRequest(request, "Introduce local for ", "all occurrences"),
        };

        return _selectionStager.StageReplayCodeActionAsync(replayRequest, context, cancellationToken);
    }

    private static ReplayCodeActionRequest CreateReplayRequest(IntroduceVariableRequest request, string titleStartsWith, string? titleDoesNotContain = null)
    {
        return new ReplayCodeActionRequest
        {
            Location = request.Selection,
            ExpectedSnapshot = request.ExpectedSnapshot,
            TitleStartsWith = titleStartsWith,
            TitleDoesNotContain = titleDoesNotContain,
        };
    }
}
