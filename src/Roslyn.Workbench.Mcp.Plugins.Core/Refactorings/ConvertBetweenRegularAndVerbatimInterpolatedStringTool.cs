using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class ConvertBetweenRegularAndVerbatimInterpolatedStringTool : MutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimInterpolatedStringCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-between-regular-and-verbatim-interpolated-string",
        Title = "Convert Between Regular And Verbatim Interpolated String",
        Description = "Converts a supported interpolated string between regular and verbatim forms through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertBetweenRegularAndVerbatimInterpolatedStringTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return context.ToolExecutionServices.ReplayCodeActionExecutor.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId);
    }
}
