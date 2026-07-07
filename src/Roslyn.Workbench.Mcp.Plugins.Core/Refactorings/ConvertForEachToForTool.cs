using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class ConvertForEachToForTool : MutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertForEachToFor.CSharpConvertForEachToForCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-foreach-to-for",
        Title = "Convert Foreach To For",
        Description = "Converts a supported foreach loop to a for loop through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertForEachToForTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return context.ToolExecutionServices.ReplayCodeActionExecutor.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId,
            title: "Convert to 'for'");
    }
}
