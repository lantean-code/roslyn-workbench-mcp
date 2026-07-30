namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginCollisionPolicy : IPluginCollisionPolicy
{
    public string? FindProtectedToolCollision(PreparedCatalogPlugin plugin, IReadOnlySet<string> protectedToolNames)
    {
        foreach (var tool in plugin.Preparation.Tools)
        {
            var toolName = tool.Tool.Metadata.Name;
            if (protectedToolNames.Contains(toolName))
            {
                return toolName;
            }
        }

        return null;
    }

    public IReadOnlySet<string> FindDuplicateExternalPluginIds(IReadOnlyList<PluginPackageDiscoveryResult> discoveryResults)
    {
        var observedPluginIds = new HashSet<string>(StringComparer.Ordinal);
        var duplicatePluginIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var discoveryResult in discoveryResults)
        {
            var pluginId = discoveryResult.Candidate?.EntryPoint.PluginId;
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                continue;
            }

            if (!observedPluginIds.Add(pluginId))
            {
                duplicatePluginIds.Add(pluginId);
            }
        }

        return duplicatePluginIds;
    }

    public IReadOnlySet<string> FindExternalToolCollisions(
        IReadOnlyList<PreparedCatalogPlugin> plugins,
        IReadOnlySet<string> protectedToolNames)
    {
        var collisions = new HashSet<string>(StringComparer.Ordinal);
        var pluginIdsByToolName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var plugin in plugins)
        {
            var pluginId = plugin.Metadata.PluginId;
            foreach (var tool in plugin.Preparation.Tools)
            {
                var toolName = tool.Tool.Metadata.Name;
                if (protectedToolNames.Contains(toolName))
                {
                    collisions.Add(pluginId);
                }

                if (!pluginIdsByToolName.TryGetValue(toolName, out var pluginIds))
                {
                    pluginIds = new HashSet<string>(StringComparer.Ordinal);
                    pluginIdsByToolName.Add(toolName, pluginIds);
                }

                pluginIds.Add(pluginId);
            }
        }

        foreach (var pluginIds in pluginIdsByToolName.Values)
        {
            if (pluginIds.Count > 1)
            {
                collisions.UnionWith(pluginIds);
            }
        }

        return collisions;
    }
}
