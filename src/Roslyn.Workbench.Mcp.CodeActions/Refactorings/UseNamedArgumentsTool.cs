using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class UseNamedArgumentsTool : CodeActionMutationToolHandler<UseNamedArgumentsRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider";

    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "use-named-arguments",
        Title = "Use Named Arguments",
        Description = "Adds a supported argument name through Roslyn refactoring composition.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new UseNamedArgumentsTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(UseNamedArgumentsRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var titleDoesNotContain = request.IncludeTrailingArguments ? null : "including trailing arguments";
        var titleStartsWith = request.IncludeTrailingArguments
            ? "Add argument name '"
            : "Add argument name '";

        return context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            ProviderId,
            titleStartsWith: titleStartsWith,
            titleDoesNotContain: titleDoesNotContain);
    }
}
