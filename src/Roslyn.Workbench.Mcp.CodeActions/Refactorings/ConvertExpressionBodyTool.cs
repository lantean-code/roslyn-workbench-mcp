using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertExpressionBodyTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string UseExpressionBodyProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider";
    private const string UseExpressionBodyForLambdaProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider";

    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "convert-expression-body",
        Title = "Convert Expression Body",
        Description = "Stages a supported Roslyn block-body or expression-body conversion at the selected declaration.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertExpressionBodyTool());
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(LocationRefactoringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var result = await context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            UseExpressionBodyProviderId).ConfigureAwait(false);
        if (!ShouldTryLambdaProvider(result))
        {
            return result;
        }

        return await context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            UseExpressionBodyForLambdaProviderId).ConfigureAwait(false);
    }

    private static bool ShouldTryLambdaProvider(CodeActionExecutionResult<WorkspaceMutationCandidate> result)
    {
        return result.Outcome == CodeActionExecutionOutcome.Rejected
            && string.Equals(result.Error?.Code, "CodeActionUnavailable", StringComparison.Ordinal);
    }
}
