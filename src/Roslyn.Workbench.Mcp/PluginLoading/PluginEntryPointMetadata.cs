namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Contains the identity and compatibility metadata read from one plugin entry-point attribute.
/// </summary>
internal sealed record PluginEntryPointMetadata
{
    /// <summary>
    /// Gets the stable plugin identifier used for collision detection and status.
    /// </summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user-facing plugin name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Roslyn Workbench plugin API version declared by the entry point.
    /// </summary>
    public string SupportedApiVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the plugin assembly's informational version.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets the metadata name of the type carrying the plugin entry-point attribute.
    /// </summary>
    public string EntryTypeName { get; init; } = string.Empty;
}
