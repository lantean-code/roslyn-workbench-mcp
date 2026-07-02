using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class ConvertTryCastToDirectCastTool : MutationToolHandler<LocationRefactoringRequest, MutationProposal>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertTryCastToDirectCastCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-try-cast-to-direct-cast",
        Title = "Convert Try Cast To Direct Cast",
        Description = "Converts a supported as-expression to a cast expression through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertTryCastToDirectCastTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return ToolExecutionHelpers.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId,
            title: "Change to cast");
    }
}
