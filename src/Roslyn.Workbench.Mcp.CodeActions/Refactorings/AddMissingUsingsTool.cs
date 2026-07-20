using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class AddMissingUsingsTool : CodeActionMutationToolHandler<AddMissingUsingsRequest>
{
    private const string AddImportProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider";

    private readonly ICodeActionScopedFixService _scopedFixService;

    public AddMissingUsingsTool(ICodeActionScopedFixService scopedFixService)
    {
        _scopedFixService = scopedFixService;
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

        return _scopedFixService.StageScopedCodeFixAsync(new ScopedCodeFixRequest
        {
            Scope = request.Scope,
            ExpectedSnapshot = request.ExpectedSnapshot,
            DiagnosticIds =
            [
                "CS0103",
                "CS0246",
            ],
            ProviderId = AddImportProviderId,
        }, context, cancellationToken);
    }
}
