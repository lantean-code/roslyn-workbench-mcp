using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class AddAwaitTool : CodeActionMutationToolHandler<AddAwaitRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider";

    private readonly ICodeActionReplayService _replayService;

    public AddAwaitTool(ICodeActionReplayService replayService)
    {
        _replayService = replayService;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(AddAwaitRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var title = request.Kind == AddAwaitKind.AwaitConfigureAwaitFalse
            ? "Add 'await' and 'ConfigureAwait(false)'"
            : "Add 'await'";
        var actionPath = request.Kind == AddAwaitKind.AwaitConfigureAwaitFalse
            ? new[] { 1 }
            : new[] { 0 };

        return _replayService.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            ProviderId,
            title: title,
            actionPath: actionPath);
    }
}
