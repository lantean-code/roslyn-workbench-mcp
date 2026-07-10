namespace Roslyn.Workbench.Mcp.Plugins.Execution;

public sealed record RegisteredTool
{
    public PluginMetadata Plugin { get; init; } = new();

    public ToolRegistrationMetadata Metadata { get; init; } = new();

    public ToolKind Kind { get; init; }

    public Type RequestType { get; init; } = typeof(object);

    public Type ResponseType { get; init; } = typeof(object);
}
