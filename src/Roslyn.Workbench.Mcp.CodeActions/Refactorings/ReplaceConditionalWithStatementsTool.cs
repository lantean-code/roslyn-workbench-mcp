using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ReplaceConditionalWithStatementsTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ReplaceConditionalWithStatements.CSharpReplaceConditionalWithStatementsCodeRefactoringProvider";

    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "replace-conditional-with-statements",
        Title = "Replace Conditional With Statements",
        Description = "Rewrites a supported conditional expression into statements through Roslyn refactoring composition.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ReplaceConditionalWithStatementsTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            ProviderId,
            title: "Replace conditional expression with statements");
    }
}
