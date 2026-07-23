namespace Roslyn.Workbench.Mcp.Plugins.Registration;

/// <summary>
/// Describes a plugin entry point and its compatibility contract.
/// </summary>
internal sealed record PluginMetadata
{
    /// <summary>
    /// Gets the stable plugin identifier.
    /// </summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name of the plugin.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the semantic version of the plugin.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets the supported public plugin API version.
    /// </summary>
    public string SupportedApiVersion { get; init; } = string.Empty;
}
