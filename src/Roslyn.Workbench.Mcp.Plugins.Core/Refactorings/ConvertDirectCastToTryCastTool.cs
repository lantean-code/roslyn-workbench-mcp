using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class ConvertDirectCastToTryCastTool : MutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertDirectCastToTryCastCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-direct-cast-to-try-cast",
        Title = "Convert Direct Cast To Try Cast",
        Description = "Converts a supported cast expression to an as-expression through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertDirectCastToTryCastTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return context.ToolExecutionServices.ReplayCodeActionExecutor.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId,
            title: "Change to 'as' expression");
    }
}
