using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class RemoveUnusedUsingsTool : CodeActionMutationToolHandler<RemoveUnusedUsingsRequest>
{
    private const string FixableDiagnosticId = "RemoveUnnecessaryImportsFixable";

    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "remove-unused-usings",
        Title = "Remove Unused Usings",
        Description = "Removes unused using directives across a selected scope through Roslyn code-fix composition.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new RemoveUnusedUsingsTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteCoreAsync(RemoveUnusedUsingsRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageScopedCodeFixAsync(new ScopedCodeFixRequest
        {
            Scope = request.Scope,
            ExpectedSnapshot = request.ExpectedSnapshot,
            DiagnosticIds = [FixableDiagnosticId],
            Title = "Remove unnecessary usings",
            SyntheticDiagnosticId = FixableDiagnosticId,
        }, cancellationToken);
    }
}
