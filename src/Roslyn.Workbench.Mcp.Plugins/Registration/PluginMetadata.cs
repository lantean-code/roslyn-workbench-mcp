namespace Roslyn.Workbench.Mcp.Plugins.Registration;

/// <summary>
/// Describes a plugin entry point and its compatibility contract.
/// </summary>
internal sealed record PluginMetadata
{
    /// <summary>
    /// Gets the stable plugin identifier.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets the display name of the plugin.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the semantic version of the plugin.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the supported public plugin API version.
    /// </summary>
    public required string SupportedApiVersion { get; init; }
}
