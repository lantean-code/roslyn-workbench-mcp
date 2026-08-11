namespace Roslyn.Workbench.Mcp.Plugins.Configuration;

internal sealed record ConfiguredToolDefinition
{
    public required Type HandlerType { get; init; }

    public required ToolKind Kind { get; init; }

    public required IToolConfigurationBuilderState Builder { get; init; }
}
