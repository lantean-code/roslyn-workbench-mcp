using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertLocalFunctionToMethodTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ConvertLocalFunctionToMethod.CSharpConvertLocalFunctionToMethodCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-local-function-to-method",
        Title = "Convert Local Function To Method",
        Description = "Converts a supported local function to a method through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertLocalFunctionToMethodTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            ProviderId,
            title: "Convert to method");
    }
}
