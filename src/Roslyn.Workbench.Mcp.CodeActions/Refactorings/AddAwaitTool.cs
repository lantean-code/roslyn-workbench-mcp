using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class AddAwaitTool : CodeActionMutationToolHandler<AddAwaitRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "add-await",
        Title = "Add Await",
        Description = "Stages one supported add-await refactoring through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new AddAwaitTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(AddAwaitRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var title = request.Kind == AddAwaitKind.AwaitConfigureAwaitFalse
            ? "Add 'await' and 'ConfigureAwait(false)'"
            : "Add 'await'";
        var actionPath = request.Kind == AddAwaitKind.AwaitConfigureAwaitFalse
            ? new[] { 1 }
            : new[] { 0 };

        return context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            ProviderId,
            title: title,
            actionPath: actionPath);
    }
}
