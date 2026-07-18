using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class LoadedPluginPreparer : ILoadedPluginPreparer
{
    private readonly IPluginComposer _composer;
    private readonly IPluginConfigurationPreparer _configurationPreparer;

    public LoadedPluginPreparer(
        IPluginComposer composer,
        IPluginConfigurationPreparer configurationPreparer)
    {
        _composer = composer;
        _configurationPreparer = configurationPreparer;
    }

    public PreparedCatalogPlugin Prepare(Assembly assembly, PluginEntryPointMetadata entryPoint)
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
            Preparation = _configurationPreparer.Prepare(metadata, configuration),
        };
    }

    private static PreparedCatalogPlugin CreateCompositionFailure(PluginMetadata metadata, string error)
    {
        return new PreparedCatalogPlugin
        {
            Metadata = metadata,
            Preparation = new PluginPreparationResult
            {
                Diagnostics =
                [
                    PluginCatalogStatusFactory.CreateDiagnostic(
                        PluginDiagnosticIds.Composition,
                        DiagnosticSeverity.Error,
                        error),
                ],
            },
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
