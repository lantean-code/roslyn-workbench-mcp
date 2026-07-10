using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class UseExplicitTypeTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseExplicitType.UseExplicitTypeCodeRefactoringProvider";

    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "use-explicit-type",
        Title = "Use Explicit Type",
        Description = "Converts a supported declaration to an explicit type through Roslyn refactoring composition.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new UseExplicitTypeTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            ProviderId,
            title: "Use explicit type");
    }
}
