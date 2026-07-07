using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class ConvertToRecordTool : MutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToRecord.CSharpConvertToRecordRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-to-record",
        Title = "Convert To Record",
        Description = "Converts a supported class declaration to a record through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertToRecordTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return context.ToolExecutionServices.ReplayCodeActionExecutor.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId,
            title: "Convert to positional record");
    }
}
