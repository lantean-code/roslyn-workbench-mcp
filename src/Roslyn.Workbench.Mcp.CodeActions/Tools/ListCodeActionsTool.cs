using Roslyn.Workbench.Mcp.CodeActions.Contracts;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class ListCodeActionsTool : CodeActionQueryToolHandler<ListCodeActionsRequest, CodeActionListData>
{
    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "list-code-actions",
        Title = "List Code Actions",
        Description = "Lists applicable code actions and code fixes at a target location.",
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new ListCodeActionsTool());
    }

    protected override ValueTask<CodeActionExecutionResult<CodeActionListData>> ExecuteCoreAsync(ListCodeActionsRequest request, ICodeActionQueryContext context, CancellationToken cancellationToken)
    {
        return context.ListCodeActionsAsync(request, cancellationToken);
    }
}
