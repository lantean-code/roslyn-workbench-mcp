namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginPackageDiscovery
{
    IReadOnlyList<PluginPackageDiscoveryResult> Discover(IReadOnlyList<string> searchRoots);
}
