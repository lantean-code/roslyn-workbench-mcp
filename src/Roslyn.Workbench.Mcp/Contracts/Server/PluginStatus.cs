namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the load status of one plugin.
/// </summary>
internal sealed record PluginStatus
{
    /// <summary>
    /// Gets the stable plugin identifier.
    /// </summary>
    [Description("The stable plugin identifier, when supplied by the plugin.")]
    public string? PluginId { get; init; }

    /// <summary>
    /// Gets the display name of the plugin.
    /// </summary>
    [Description("The display name, when supplied by the plugin.")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the semantic version of the plugin.
    /// </summary>
    [Description("The semantic version, when supplied by the plugin.")]
    public string? Version { get; init; }

    /// <summary>
    /// Gets the supported plugin API version.
    /// </summary>
    [Description("The supported plugin API version, when supplied by the plugin.")]
    public string? SupportedApiVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the plugin is enabled.
    /// </summary>
    [Description("Whether the plugin is enabled.")]
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the plugin load diagnostics.
    /// </summary>
    [Description("The plugin load diagnostics.")]
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];
}
