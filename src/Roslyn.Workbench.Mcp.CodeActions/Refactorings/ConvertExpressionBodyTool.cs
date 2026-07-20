using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertExpressionBodyTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string UseExpressionBodyProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider";
    private const string UseExpressionBodyForLambdaProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider";

    private readonly ICodeActionReplayService _replayService;

    public ConvertExpressionBodyTool(ICodeActionReplayService replayService)
    {
        _replayService = replayService;
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(LocationRefactoringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var result = await _replayService.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            UseExpressionBodyProviderId);

        if (!ShouldTryLambdaProvider(result))
        {
            return result;
        }

        return await _replayService.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            UseExpressionBodyForLambdaProviderId);
    }

    private static bool ShouldTryLambdaProvider(CodeActionExecutionResult<WorkspaceMutationCandidate> result)
    {
        return result.Outcome == CodeActionExecutionOutcome.Rejected
            && string.Equals(result.Error?.Code, "CodeActionUnavailable", StringComparison.Ordinal);
    }
}
