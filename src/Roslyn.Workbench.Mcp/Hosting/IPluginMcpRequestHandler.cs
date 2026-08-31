namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Handles MCP discovery and invocation for the immutable loaded-plugin catalogue.
/// </summary>
internal interface IPluginMcpRequestHandler
{
    /// <summary>
    /// Lists the MCP tools published by the loaded plugins.
    /// </summary>
    /// <param name="context">The active MCP request and server context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing metadata for every published plugin tool.</returns>
    ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Invokes a published plugin tool.
    /// </summary>
    /// <param name="context">The active MCP request and server context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing the selected tool's structured MCP result.</returns>
    ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken);
}
