using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class UseNamedArgumentsTool : CodeActionMutationToolHandler<UseNamedArgumentsRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider";

    private readonly ICodeActionReplayService _replayService;

    public UseNamedArgumentsTool(ICodeActionReplayService replayService)
    {
        _replayService = replayService;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(UseNamedArgumentsRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var titleDoesNotContain = request.IncludeTrailingArguments ? null : "including trailing arguments";
        var titleStartsWith = request.IncludeTrailingArguments
            ? "Add argument name '"
            : "Add argument name '";

        return _replayService.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            ProviderId,
            titleStartsWith: titleStartsWith,
            titleDoesNotContain: titleDoesNotContain);
    }
}
