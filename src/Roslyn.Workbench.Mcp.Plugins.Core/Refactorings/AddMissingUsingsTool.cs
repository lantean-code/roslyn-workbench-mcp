using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class AddMissingUsingsTool : MutationToolHandler<AddMissingUsingsRequest, MutationProposal>
{
    private const string AddImportProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "add-missing-usings",
        Title = "Add Missing Usings",
        Description = "Adds missing using directives across a selected scope through Roslyn code-fix composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new AddMissingUsingsTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(AddMissingUsingsRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        if (request.PreferGlobalUsings)
        {
            return ValueTask.FromResult(context.ToolExecutionServices.ResultShaper.Rejected<MutationProposal>("UnsupportedOption", "The preferGlobalUsings option is not supported by the current Roslyn add-import backend."));
        }

        return context.CodeActionService.StageScopedCodeFixAsync(new ScopedCodeFixRequest
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
