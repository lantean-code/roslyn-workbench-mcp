namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the load status of one plugin.
/// </summary>
internal sealed record PluginStatus
{
    /// <summary>
    /// The stable plugin identifier, when supplied by the plugin.
    /// </summary>
    [Description("The stable plugin identifier, when supplied by the plugin.")]
    public string? PluginId { get; init; }

    /// <summary>
    /// The display name, when supplied by the plugin.
    /// </summary>
    [Description("The display name, when supplied by the plugin.")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// The semantic version, when supplied by the plugin.
    /// </summary>
    [Description("The semantic version, when supplied by the plugin.")]
    public string? Version { get; init; }

    /// <summary>
    /// The supported plugin API version, when supplied by the plugin.
    /// </summary>
    [Description("The supported plugin API version, when supplied by the plugin.")]
    public string? SupportedApiVersion { get; init; }

    /// <summary>
    /// Whether the plugin is enabled.
    /// </summary>
    [Description("Whether the plugin is enabled.")]
    public bool Enabled { get; init; }

    /// <summary>
    /// The plugin load diagnostics.
    /// </summary>
    [Description("The plugin load diagnostics.")]
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];
}
