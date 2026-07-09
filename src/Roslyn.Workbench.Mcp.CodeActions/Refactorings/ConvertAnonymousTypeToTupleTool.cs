using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertAnonymousTypeToTupleTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToTupleCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-anonymous-type-to-tuple",
        Title = "Convert Anonymous Type To Tuple",
        Description = "Converts a supported anonymous type to a tuple through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertAnonymousTypeToTupleTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            ProviderId,
            title: "Convert to tuple");
    }
}
