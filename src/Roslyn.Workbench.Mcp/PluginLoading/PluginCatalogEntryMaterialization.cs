namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Contains one plugin's published tools, catalogue status and service-provider lifetime.
/// </summary>
internal sealed record PluginCatalogEntryMaterialization
{
    /// <summary>
    /// Gets the runtime tool wrappers created for the plugin.
    /// </summary>
    public IReadOnlyList<IRegisteredPluginTool> Tools { get; init; } = [];

    /// <summary>
    /// Gets the enabled or disabled catalogue status for the plugin.
    /// </summary>
    public required PluginStatus Status { get; init; }

    /// <summary>
    /// Gets the plugin service provider that must be disposed with the catalogue.
    /// </summary>
    public IDisposable? ServiceProviderLifetime { get; init; }
}
