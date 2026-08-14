using ModelContextProtocol;

namespace Roslyn.Workbench.Mcp.Hosting;

internal sealed class PluginMcpRequestHandler : IPluginMcpRequestHandler
{
    private readonly IPluginCatalogState _catalogState;

    public PluginMcpRequestHandler(IPluginCatalogState catalogState)
    {
        _catalogState = catalogState;
    }

    public ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<Tool> tools;
        if (context.Params?.Cursor is null)
        {
            var runtimeCatalog = _catalogState.Current;
            tools = runtimeCatalog.Tools.Values.Select(static tool => tool.ProtocolTool).ToList();
        }
        else
        {
            tools = [];
        }

        return ValueTask.FromResult(new ListToolsResult
        {
            Tools = tools,
        });
    }

#pragma warning disable MCPEXP001

    public ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        var runtimeCatalog = _catalogState.Current;
        var toolName = context.Params?.Name;
        if (toolName is null || !runtimeCatalog.Tools.TryGetValue(toolName, out var tool))
        {
            throw new RoslynWorkbenchMcpProtocolException(
                $"Tool '{toolName}' is not registered.",
                McpErrorCode.InvalidParams);
        }

        if (context.Params?.Task is not null)
        {
            throw new RoslynWorkbenchMcpProtocolException(
                $"Tool '{toolName}' does not support task-augmented execution.",
                McpErrorCode.InvalidParams);
        }

        return tool.InvokeAsync(context, cancellationToken);
    }

#pragma warning restore MCPEXP001
}
