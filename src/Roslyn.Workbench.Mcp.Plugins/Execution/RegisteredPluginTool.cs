namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed record RegisteredPluginTool
{
    public RegisteredTool Tool { get; init; } = new();

    public IPluginToolRuntime Runtime { get; init; } = NullPluginToolRuntime.Instance;
}
