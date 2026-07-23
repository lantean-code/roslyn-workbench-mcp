using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class AddMissingUsingsTool : CodeActionMutationToolHandler<AddMissingUsingsRequest>
{
    private const string _addImportProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider";

    private readonly IScopedCodeFixStager _scopedFixStager;

    public AddMissingUsingsTool(IScopedCodeFixStager scopedFixStager)
    {
        _scopedFixStager = scopedFixStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(AddMissingUsingsRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (request.PreferGlobalUsings)
        {
            var rejection = CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>(
                "UnsupportedOption",
                "The preferGlobalUsings option is not supported by the current Roslyn add-import backend.");

            return ValueTask.FromResult(rejection);
        }

        return _scopedFixStager.StageScopedCodeFixAsync(new ScopedCodeFixRequest
        {
            Scope = request.Scope,
            ExpectedSnapshot = request.ExpectedSnapshot,
            DiagnosticIds =
            [
                "CS0103",
                "CS0246",
            ],
            ProviderId = _addImportProviderId,
        }, context, cancellationToken);
    }
}
