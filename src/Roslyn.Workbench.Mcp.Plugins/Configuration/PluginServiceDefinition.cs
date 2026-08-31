namespace Roslyn.Workbench.Mcp.Plugins.Configuration;

/// <summary>
/// Describes a singleton service-to-implementation mapping requested by a plugin.
/// </summary>
internal sealed record PluginServiceDefinition
{
    /// <summary>
    /// Gets the contract type resolved from the plugin service provider.
    /// </summary>
    public required Type ServiceType { get; init; }

    /// <summary>
    /// Gets the concrete type created for the service contract.
    /// </summary>
    public required Type ImplementationType { get; init; }
}
