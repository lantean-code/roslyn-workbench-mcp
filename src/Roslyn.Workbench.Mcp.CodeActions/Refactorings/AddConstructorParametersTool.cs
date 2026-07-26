using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class AddConstructorParametersTool : CodeActionMutationToolHandler<AddConstructorParametersRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.AddConstructorParametersFromMembers.AddConstructorParametersFromMembersCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public AddConstructorParametersTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(
        AddConstructorParametersRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        var actionPath = request.Kind == AddConstructorParametersKind.Required
            ? new[] { 0 }
            : [1];

        return _selectionStager.StageSelectionAsync(
            request.Members,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            _providerId,
            actionPath: actionPath);
    }
}
