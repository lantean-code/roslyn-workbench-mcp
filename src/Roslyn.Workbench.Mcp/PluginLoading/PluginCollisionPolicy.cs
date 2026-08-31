namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Detects plugin and tool identifiers that cannot be published together.
/// </summary>
internal sealed class PluginCollisionPolicy : IPluginCollisionPolicy
{
    /// <summary>
    /// Finds the first tool in a plugin that conflicts with a host-owned or already protected name.
    /// </summary>
    /// <param name="plugin">The plugin instance being registered or inspected.</param>
    /// <param name="protectedToolNames">The tool names reserved by the host.</param>
    /// <returns>The conflicting tool name, or <see langword="null"/> when no protected name is used.</returns>
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

    /// <summary>
    /// Finds plugin identifiers declared by more than one external package candidate.
    /// </summary>
    /// <param name="discoveryResults">The plugin discovery results to include in the catalogue.</param>
    /// <returns>The duplicated plugin identifiers using ordinal comparison.</returns>
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

    /// <summary>
    /// Finds external plugins whose tool names are duplicated or conflict with a protected name.
    /// </summary>
    /// <param name="plugins">The prepared plugins whose tool names are checked for collisions.</param>
    /// <param name="protectedToolNames">The tool names reserved by the host.</param>
    /// <returns>The plugin identifiers that must be disabled because at least one tool name collides.</returns>
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
