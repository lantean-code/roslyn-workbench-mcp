namespace Roslyn.Workbench.Mcp;

internal sealed class PluginCollisionPolicy : IPluginCollisionPolicy
{
    public string? FindProtectedToolCollision(PreparedCatalogPlugin plugin, IReadOnlySet<string> protectedToolNames)
    {
        return GetToolNames(plugin).FirstOrDefault(protectedToolNames.Contains);
    }

    public IReadOnlySet<string> FindDuplicateExternalPluginIds(IReadOnlyList<PluginPackageDiscoveryResult> discoveryResults)
    {
        return discoveryResults
            .Select(static result => result.Candidate)
            .OfType<PluginPackageCandidate>()
            .Select(static candidate => candidate.EntryPoint.PluginId)
            .Where(static pluginId => !string.IsNullOrWhiteSpace(pluginId))
            .GroupBy(static pluginId => pluginId, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlySet<string> FindExternalToolCollisions(
        IReadOnlyList<PreparedCatalogPlugin> plugins,
        IReadOnlySet<string> protectedToolNames)
    {
        var collisions = plugins
            .Where(plugin => GetToolNames(plugin).Any(protectedToolNames.Contains))
            .Select(static plugin => plugin.Metadata.PluginId)
            .ToHashSet(StringComparer.Ordinal);

        var sharedNames = plugins
            .SelectMany(plugin => GetToolNames(plugin).Select(toolName => (plugin.Metadata.PluginId, ToolName: toolName)))
            .GroupBy(static item => item.ToolName, StringComparer.Ordinal)
            .Where(static group => group.Select(static item => item.PluginId).Distinct(StringComparer.Ordinal).Count() > 1);
        foreach (var sharedName in sharedNames)
        {
            collisions.UnionWith(sharedName.Select(static item => item.PluginId));
        }

        return collisions;
    }

    private static IEnumerable<string> GetToolNames(PreparedCatalogPlugin plugin)
    {
        return plugin.Preparation.Tools.Select(static tool => tool.Tool.Metadata.Name);
    }
}
