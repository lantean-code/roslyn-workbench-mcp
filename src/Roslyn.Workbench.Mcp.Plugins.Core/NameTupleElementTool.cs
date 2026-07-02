using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class NameTupleElementTool : MutationToolHandler<LocationRefactoringRequest, MutationProposal>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.NameTupleElement.CSharpNameTupleElementCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "name-tuple-element",
        Title = "Name Tuple Element",
        Description = "Adds a supported tuple element name through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new NameTupleElementTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return ToolExecutionHelpers.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId,
            titleStartsWith: "Add tuple element name '");
    }
}
