using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class UseNamedArgumentsTool : CodeActionMutationToolHandler<UseNamedArgumentsRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public UseNamedArgumentsTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(UseNamedArgumentsRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var titleDoesNotContain = request.IncludeTrailingArguments ? null : "including trailing arguments";
        var titleStartsWith = request.IncludeTrailingArguments
            ? "Add argument name '"
            : "Add argument name '";

        return _selectionStager.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            _providerId,
            titleStartsWith: titleStartsWith,
            titleDoesNotContain: titleDoesNotContain);
    }
}
