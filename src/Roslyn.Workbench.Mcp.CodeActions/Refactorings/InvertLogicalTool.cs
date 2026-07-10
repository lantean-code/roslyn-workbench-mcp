using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class InvertLogicalTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.InvertLogical.CSharpInvertLogicalCodeRefactoringProvider";

    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "invert-logical",
        Title = "Invert Logical",
        Description = "Inverts a supported logical expression through Roslyn refactoring composition.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new InvertLogicalTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            ProviderId,
            titleStartsWith: "Replace '");
    }
}
