namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed record RegisteredPluginTool
{
    public RegisteredTool Tool { get; init; } = new();

    public IPluginToolExecutionAdapter ExecutionAdapter { get; init; } = NullPluginToolExecutionAdapter.Instance;
}
