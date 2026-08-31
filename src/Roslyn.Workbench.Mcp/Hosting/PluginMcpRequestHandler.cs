using ModelContextProtocol;

namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Serves MCP tool discovery and invocation from the immutable runtime plugin catalogue.
/// </summary>
internal sealed class PluginMcpRequestHandler : IPluginMcpRequestHandler
{
    private readonly IPluginCatalogState _catalogState;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginMcpRequestHandler"/> class.
    /// </summary>
    /// <param name="catalogState">The published plugin catalogue used to resolve tool invocations.</param>
    public PluginMcpRequestHandler(IPluginCatalogState catalogState)
    {
        _catalogState = catalogState;
    }

    /// <summary>
    /// Lists the MCP tools published by the loaded plugins.
    /// </summary>
    /// <param name="context">The active MCP list-tools request and server context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing the first and only page of published plugin tools.</returns>
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

    /// <summary>
    /// Invokes a published plugin tool.
    /// </summary>
    /// <param name="context">The active MCP call-tool request and server context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing the selected plugin tool's MCP result.</returns>
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
