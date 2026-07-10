using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class AddDebuggerDisplayTool : CodeActionMutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.AddDebuggerDisplay.CSharpAddDebuggerDisplayCodeRefactoringProvider";

    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "add-debugger-display",
        Title = "Add Debugger Display",
        Description = "Adds a DebuggerDisplay attribute through Roslyn refactoring composition.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new AddDebuggerDisplayTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            cancellationToken,
            ProviderId,
            title: "Add 'DebuggerDisplay' attribute");
    }
}
