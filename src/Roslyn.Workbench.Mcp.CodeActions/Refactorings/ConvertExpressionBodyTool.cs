using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertExpressionBodyTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string UseExpressionBodyProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider";
    private const string UseExpressionBodyForLambdaProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public ConvertExpressionBodyTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(LocationRefactoringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var result = await _selectionStager.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            UseExpressionBodyProviderId);

        if (!ShouldTryLambdaProvider(result))
        {
            return result;
        }

        return await _selectionStager.StageSelectionAsync(
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
