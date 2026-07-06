using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class IntroduceVariableTool : MutationToolHandler<IntroduceVariableRequest, MutationProposal>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "introduce-variable",
        Title = "Introduce Variable",
        Description = "Stages one supported Roslyn introduce-variable leaf action through refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new IntroduceVariableTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(IntroduceVariableRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        if (request.Selection is null)
        {
            return ValueTask.FromResult(context.ToolExecutionServices.ResultShaper.Rejected<MutationProposal>("InvalidRequest", "A location selector is required."));
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

        return context.StageReplayCodeActionAsync(replayRequest, cancellationToken);
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
