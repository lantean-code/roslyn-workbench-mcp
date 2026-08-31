namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Combines validated plugin identity with its prepared tool and service registrations.
/// </summary>
internal sealed record PreparedCatalogPlugin
{
    /// <summary>
    /// Gets the plugin identity published in catalogue status.
    /// </summary>
    public required PluginMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the validated registrations and diagnostics produced during plugin configuration.
    /// </summary>
    public PluginPreparationResult Preparation { get; init; } = new();
}
