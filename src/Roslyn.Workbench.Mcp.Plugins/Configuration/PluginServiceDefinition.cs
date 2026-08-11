namespace Roslyn.Workbench.Mcp.Plugins.Configuration;

internal sealed record PluginServiceDefinition
{
    public required Type ServiceType { get; init; }

    public required Type ImplementationType { get; init; }
}
