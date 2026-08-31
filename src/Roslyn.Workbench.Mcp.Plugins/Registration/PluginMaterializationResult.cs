namespace Roslyn.Workbench.Mcp.Plugins.Registration;

/// <summary>
/// Carries materialized tool registrations and ownership of their plugin service provider.
/// </summary>
internal sealed record PluginMaterializationResult
{
    /// <summary>
    /// Gets the strongly typed tool registrations created for the plugin.
    /// </summary>
    public IReadOnlyList<IRegisteredPluginTool> Tools { get; init; } = [];

    /// <summary>
    /// Gets the diagnostics retained from configuration preparation.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets the lifetime that owns plugin services and handlers, when materialization created one.
    /// </summary>
    public IDisposable? ServiceProviderLifetime { get; init; }
}
