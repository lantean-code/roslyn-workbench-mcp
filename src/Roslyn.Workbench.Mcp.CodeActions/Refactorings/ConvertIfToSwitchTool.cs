using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertIfToSwitchTool : CodeActionMutationToolHandler<ConvertIfToSwitchRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertIfToSwitch.CSharpConvertIfToSwitchCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-if-to-switch",
        Title = "Convert If To Switch",
        Description = "Converts a supported if-chain to a switch statement or switch expression through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertIfToSwitchTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(ConvertIfToSwitchRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var title = request.Kind == ConvertIfToSwitchKind.Expression
            ? "Convert to 'switch' expression"
            : "Convert to 'switch' statement";

        return context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            ProviderId,
            title: title);
    }
}
