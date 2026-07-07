using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class AddImportTool : MutationToolHandler<AddImportRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "add-import",
        Title = "Add Import",
        Description = "Adds a supported using directive through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new AddImportTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(AddImportRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        var titleDoesNotContain = request.SimplifyAllOccurrences ? null : "simplify all occurrences";

        return context.ToolExecutionServices.ReplayCodeActionExecutor.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId,
            titleStartsWith: "Add 'using ",
            titleDoesNotContain: titleDoesNotContain);
    }
}
