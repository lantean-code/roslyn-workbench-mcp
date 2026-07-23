namespace Roslyn.Workbench.Mcp.Plugins.Preparation;

internal sealed record PreparedPluginTool
{
    public required Type HandlerType { get; init; }

    public required Type HandlerContract { get; init; }

    public required Func<object> HandlerFactory { get; init; }

    public required RegisteredTool Tool { get; init; }
}
