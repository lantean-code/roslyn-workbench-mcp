using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class ConvertAnonymousTypeToClassTool : MutationToolHandler<ConvertAnonymousTypeToClassRequest, MutationProposal>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-anonymous-type-to-class",
        Title = "Convert Anonymous Type To Class",
        Description = "Converts a supported anonymous type to a generated class or record through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertAnonymousTypeToClassTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(ConvertAnonymousTypeToClassRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        var title = request.Kind == ConvertAnonymousTypeToClassKind.Record
            ? "Convert to record"
            : "Convert to class";

        return ToolExecutionHelpers.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId,
            title: title);
    }
}
