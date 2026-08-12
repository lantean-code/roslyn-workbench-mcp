namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the load status of one plugin.
/// </summary>
internal sealed record PluginStatus
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
    /// Gets the supported plugin API version.
    /// </summary>
    public string SupportedApiVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the plugin is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the plugin load diagnostics.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];
}
