namespace Roslyn.Workbench.Mcp.ToolExecution.Plugins;

/// <summary>
/// Materializes registered plugin queries and mutations as executable MCP server tools.
/// </summary>
internal interface IPluginMcpServerToolFactory : IPluginToolRegistrationVisitor<McpServerTool>
{
}
