using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class AddImportTool : CodeActionMutationToolHandler<AddImportRequest>
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

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(AddImportRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var titleDoesNotContain = request.SimplifyAllOccurrences ? null : "simplify all occurrences";

        return context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            ProviderId,
            titleStartsWith: "Add 'using ",
            titleDoesNotContain: titleDoesNotContain);
    }
}
