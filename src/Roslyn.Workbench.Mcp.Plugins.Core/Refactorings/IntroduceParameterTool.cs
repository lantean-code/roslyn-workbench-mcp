using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class IntroduceParameterTool : MutationToolHandler<IntroduceParameterRequest, MutationProposal>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.IntroduceParameter.CSharpIntroduceParameterCodeRefactoringProvider";
    private const string UpdateCallSitesDirectlyTitle = "and update call sites directly";
    private const string IntoExtractedMethodTitle = "into extracted method to invoke at call sites";
    private const string IntoNewOverloadTitle = "into new overload";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "introduce-parameter",
        Title = "Introduce Parameter",
        Description = "Promotes a selected expression to a parameter through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new IntroduceParameterTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(IntroduceParameterRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        if (request.Selection is null)
        {
            return ValueTask.FromResult(context.ToolExecutionServices.ResultShaper.Rejected<MutationProposal>("InvalidRequest", "A location selector is required."));
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

        return context.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = request.Selection,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = ProviderId,
            Title = title,
            EquivalenceKey = title,
            ActionPath = actionPath,
        }, cancellationToken);
    }
}
