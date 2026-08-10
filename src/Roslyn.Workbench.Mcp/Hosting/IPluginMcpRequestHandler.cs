namespace Roslyn.Workbench.Mcp.Hosting;

internal interface IPluginMcpRequestHandler
{
    ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> context,
        CancellationToken cancellationToken);

    ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken);
}
