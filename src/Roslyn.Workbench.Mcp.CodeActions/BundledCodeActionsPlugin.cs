namespace Roslyn.Workbench.Mcp.CodeActions;

/// <summary>
/// Registers the bundled first-party code-action plugin assembly.
/// </summary>
public sealed class BundledCodeActionsPlugin : IRoslynPlugin
{
    /// <summary>
    /// Gets the bundled plugin metadata.
    /// </summary>
    public PluginMetadata Metadata => new()
    {
        PluginId = "roslyn.workbench.codeactions",
        DisplayName = "Roslyn Workbench Code Actions",
        Version = "1.0.0",
        SupportedApiVersion = PluginApiVersions.V1,
    };

    /// <summary>
    /// Registers bundled first-party code-action tools.
    /// </summary>
    /// <param name="registry">The plugin registry.</param>
    public void Register(IPluginRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        BundledCodeActionToolRegistrar.RegisterAll(registry);
    }
}
