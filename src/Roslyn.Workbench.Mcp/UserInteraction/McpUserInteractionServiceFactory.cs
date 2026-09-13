namespace Roslyn.Workbench.Mcp.UserInteraction;

/// <summary>
/// Creates MCP SDK-backed user interaction adapters.
/// </summary>
internal sealed class McpUserInteractionServiceFactory : IMcpUserInteractionServiceFactory
{
    /// <inheritdoc/>
    public IUserInteractionService Create(McpServer server)
    {
        return new McpUserInteractionService(server);
    }
}
