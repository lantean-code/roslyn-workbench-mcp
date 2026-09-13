namespace Roslyn.Workbench.Mcp.UserInteraction;

/// <summary>
/// Creates a protocol-neutral interaction service for the active MCP server session.
/// </summary>
internal interface IMcpUserInteractionServiceFactory
{
    /// <summary>
    /// Creates an interaction service backed by the supplied MCP server session.
    /// </summary>
    /// <param name="server">The active MCP server session.</param>
    /// <returns>The protocol-neutral interaction service.</returns>
    IUserInteractionService Create(McpServer server);
}
