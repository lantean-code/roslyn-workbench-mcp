using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class AddAwaitTool : CodeActionMutationToolHandler<AddAwaitRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public AddAwaitTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(AddAwaitRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var title = request.Kind == AddAwaitKind.AwaitConfigureAwaitFalse
            ? "Add 'await' and 'ConfigureAwait(false)'"
            : "Add 'await'";

        var actionPath = request.Kind == AddAwaitKind.AwaitConfigureAwaitFalse
            ? 1
            : 0;

        return _selectionStager.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            ProviderId,
            title: title,
            actionPath: [actionPath]);
    }
}
