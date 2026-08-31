using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Composes a loaded assembly and validates the plugin registrations it contributes to the catalogue.
/// </summary>
internal sealed class LoadedPluginPreparer : ILoadedPluginPreparer
{
    private readonly IPluginComposer _composer;
    private readonly IPluginConfigurationPreparer _configurationPreparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadedPluginPreparer"/> class.
    /// </summary>
    /// <param name="composer">The component that composes the plugin's MEF export.</param>
    /// <param name="configurationPreparer">The component that validates and materializes registered tools and services.</param>
    public LoadedPluginPreparer(
        IPluginComposer composer,
        IPluginConfigurationPreparer configurationPreparer)
    {
        _composer = composer;
        _configurationPreparer = configurationPreparer;
    }

    /// <summary>
    /// Composes a loaded plugin and materializes its validated catalogue metadata.
    /// </summary>
    /// <param name="assembly">The loaded plugin entry assembly to compose.</param>
    /// <param name="entryPoint">The metadata read from the entry assembly before it was loaded.</param>
    /// <param name="contractAccessibility">The contract accessibility available to the plugin assembly.</param>
    /// <returns>The prepared catalogue entry, including any composition failure.</returns>
    public PreparedCatalogPlugin Prepare(
        Assembly assembly,
        PluginEntryPointMetadata entryPoint,
        PluginContractAccessibility contractAccessibility)
    {
        var configuration = new PluginConfiguration();
        var composition = _composer.Configure(assembly, configuration);
        configuration.Freeze();
        var metadata = CreatePluginMetadata(entryPoint);
        if (!composition.Succeeded)
        {
            return CreateCompositionFailure(metadata, composition.Error);
        }

        return new PreparedCatalogPlugin
        {
            Metadata = metadata,
            Preparation = _configurationPreparer.Prepare(metadata, configuration, contractAccessibility),
        };
    }

    private static PreparedCatalogPlugin CreateCompositionFailure(PluginMetadata metadata, string error)
    {
        var diagnostic = PluginCatalogStatusFactory.CreateDiagnostic(
            PluginDiagnosticIds.Composition,
            DiagnosticSeverity.Error,
            error);

        var preparation = new PluginPreparationResult
        {
            Diagnostics = [diagnostic],
        };

        return new PreparedCatalogPlugin
        {
            Metadata = metadata,
            Preparation = preparation,
        };
    }

    private static PluginMetadata CreatePluginMetadata(PluginEntryPointMetadata entryPoint)
    {
        return new PluginMetadata
        {
            PluginId = entryPoint.PluginId,
            DisplayName = entryPoint.DisplayName,
            Version = entryPoint.Version,
            SupportedApiVersion = entryPoint.SupportedApiVersion,
        };
    }
}
