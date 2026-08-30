namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed record RegisteredTool
{
    public required PluginMetadata Plugin { get; init; }

    public required ToolRegistrationMetadata Metadata { get; init; }

    public ToolKind Kind { get; init; }

    public Type RequestType { get; init; } = typeof(object);

    public Type ResponseType { get; init; } = typeof(object);
}
