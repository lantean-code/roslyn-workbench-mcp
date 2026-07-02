using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class ReverseForStatementTool : MutationToolHandler<LocationRefactoringRequest, MutationProposal>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ReverseForStatement.CSharpReverseForStatementCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "reverse-for-statement",
        Title = "Reverse For Statement",
        Description = "Reverses a supported for-statement loop through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ReverseForStatementTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return ToolExecutionHelpers.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId,
            title: "Reverse 'for' statement");
    }
}
