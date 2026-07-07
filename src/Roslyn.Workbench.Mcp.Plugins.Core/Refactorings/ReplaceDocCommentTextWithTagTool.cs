using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class ReplaceDocCommentTextWithTagTool : MutationToolHandler<LocationRefactoringRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.ReplaceDocCommentTextWithTag.CSharpReplaceDocCommentTextWithTagCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "replace-doc-comment-text-with-tag",
        Title = "Replace Doc Comment Text With Tag",
        Description = "Replaces supported XML doc comment text with a documentation tag through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ReplaceDocCommentTextWithTagTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return context.ToolExecutionServices.ReplayCodeActionExecutor.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            ProviderId,
            titleStartsWith: "Use <");
    }
}
