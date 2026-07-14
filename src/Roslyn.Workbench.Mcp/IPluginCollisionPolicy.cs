namespace Roslyn.Workbench.Mcp;

internal interface IPluginCollisionPolicy
{
    string? FindProtectedToolCollision(PreparedCatalogPlugin plugin, IReadOnlySet<string> protectedToolNames);

    IReadOnlySet<string> FindDuplicateExternalPluginIds(IReadOnlyList<PluginPackageDiscoveryResult> discoveryResults);

    IReadOnlySet<string> FindExternalToolCollisions(
        IReadOnlyList<PreparedCatalogPlugin> plugins,
        IReadOnlySet<string> protectedToolNames);
}
