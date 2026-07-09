using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class ListCodeActionsTool : CodeActionQueryToolHandler<ListCodeActionsRequest, CodeActionListData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "list-code-actions",
        Title = "List Code Actions",
        Description = "Lists applicable code actions and code fixes at a target location.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new ListCodeActionsTool());
    }

    protected override ValueTask<PluginExecutionResult<CodeActionListData>> ExecuteCoreAsync(ListCodeActionsRequest request, ICodeActionQueryContext context, CancellationToken cancellationToken)
    {
        return context.ListCodeActionsAsync(request, cancellationToken);
    }
}
