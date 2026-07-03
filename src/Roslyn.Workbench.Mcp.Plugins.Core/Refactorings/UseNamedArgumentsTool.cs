using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class UseNamedArgumentsTool : MutationToolHandler<UseNamedArgumentsRequest, MutationProposal>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "use-named-arguments",
        Title = "Use Named Arguments",
        Description = "Adds a supported argument name through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new UseNamedArgumentsTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(UseNamedArgumentsRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        var titleDoesNotContain = request.IncludeTrailingArguments ? null : "including trailing arguments";
        var titleStartsWith = request.IncludeTrailingArguments
            ? "Add argument name '"
            : "Add argument name '";

        return ToolExecutionHelpers.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId,
            titleStartsWith: titleStartsWith,
            titleDoesNotContain: titleDoesNotContain);
    }
}
