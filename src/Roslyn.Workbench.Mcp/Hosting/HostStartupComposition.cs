namespace Roslyn.Workbench.Mcp.Hosting;

internal sealed record HostStartupComposition
{
    public required StartupConfigurationSnapshot Configuration { get; init; }

    public required CodeActionCatalogSnapshot CodeActions { get; init; }

    public required PluginCatalogSnapshot Plugins { get; init; }

    public StartupOptions Options => Configuration.Options;
}
