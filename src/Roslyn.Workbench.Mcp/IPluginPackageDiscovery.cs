namespace Roslyn.Workbench.Mcp;

internal interface IPluginPackageDiscovery
{
    IReadOnlyList<PluginPackageDiscoveryResult> Discover(IReadOnlyList<string> searchRoots);
}
