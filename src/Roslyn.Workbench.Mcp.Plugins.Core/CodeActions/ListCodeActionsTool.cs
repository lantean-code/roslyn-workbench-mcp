using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.Plugins.Core.CodeActions;

internal sealed class ListCodeActionsTool : QueryToolHandler<ListCodeActionsRequest, CodeActionListData>
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

    protected override ValueTask<PluginExecutionResult<CodeActionListData>> ExecuteCoreAsync(ListCodeActionsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        return context.CodeActionService.ListCodeActionsAsync(request, context, cancellationToken);
    }
}
