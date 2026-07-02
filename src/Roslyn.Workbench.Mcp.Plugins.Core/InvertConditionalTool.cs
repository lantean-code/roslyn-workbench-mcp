using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class InvertConditionalTool : MutationToolHandler<LocationRefactoringRequest, MutationProposal>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.InvertConditional.CSharpInvertConditionalCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "invert-conditional",
        Title = "Invert Conditional",
        Description = "Inverts a supported conditional expression through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new InvertConditionalTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return ToolExecutionHelpers.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId,
            title: "Invert conditional");
    }
}
