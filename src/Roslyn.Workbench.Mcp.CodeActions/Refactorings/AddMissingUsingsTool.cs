using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class AddMissingUsingsTool : CodeActionMutationToolHandler<AddMissingUsingsRequest>
{
    private const string AddImportProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider";

    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "add-missing-usings",
        Title = "Add Missing Usings",
        Description = "Adds missing using directives across a selected scope through Roslyn code-fix composition.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new AddMissingUsingsTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteCoreAsync(AddMissingUsingsRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (request.PreferGlobalUsings)
        {
            return ValueTask.FromResult(ToolExecutionHelpers.Rejected<WorkspaceMutationProposal>("UnsupportedOption", "The preferGlobalUsings option is not supported by the current Roslyn add-import backend."));
        }

        return context.StageScopedCodeFixAsync(new ScopedCodeFixRequest
        {
            Scope = request.Scope,
            ExpectedSnapshot = request.ExpectedSnapshot,
            DiagnosticIds =
            [
                "CS0103",
                "CS0246",
            ],
            ProviderId = AddImportProviderId,
        }, cancellationToken);
    }
}
