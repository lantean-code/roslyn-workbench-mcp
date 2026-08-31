namespace Roslyn.Workbench.Mcp.Plugins.Preparation;

/// <summary>
/// Carries validated tool and service definitions together with preparation diagnostics.
/// </summary>
internal sealed record PluginPreparationResult
{
    /// <summary>
    /// Gets the tools eligible for materialization; empty when preparation found errors.
    /// </summary>
    public IReadOnlyList<PreparedPluginTool> Tools { get; init; } = [];

    /// <summary>
    /// Gets the plugin-owned singleton service mappings.
    /// </summary>
    public IReadOnlyList<PluginServiceDefinition> Services { get; init; } = [];

    /// <summary>
    /// Gets all errors and warnings found while preparing the configuration.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];
}
