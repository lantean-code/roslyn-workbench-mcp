namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Detects plugin and tool identifiers that cannot be published together.
/// </summary>
internal interface IPluginCollisionPolicy
{
    /// <summary>
    /// Finds the first tool in a plugin that conflicts with a host-owned or already protected name.
    /// </summary>
    /// <param name="plugin">The plugin instance being registered or inspected.</param>
    /// <param name="protectedToolNames">The tool names reserved by the host.</param>
    /// <returns>The conflicting tool name, or <see langword="null"/> when no protected name is used.</returns>
    string? FindProtectedToolCollision(PreparedCatalogPlugin plugin, IReadOnlySet<string> protectedToolNames);

    /// <summary>
    /// Finds plugin identifiers declared by more than one external package candidate.
    /// </summary>
    /// <param name="discoveryResults">The plugin discovery results to include in the catalogue.</param>
    /// <returns>The duplicated plugin identifiers using ordinal comparison.</returns>
    IReadOnlySet<string> FindDuplicateExternalPluginIds(IReadOnlyList<PluginPackageDiscoveryResult> discoveryResults);

    /// <summary>
    /// Finds external tool names that are duplicated or conflict with a protected name.
    /// </summary>
    /// <param name="plugins">The prepared plugins whose tool names are checked for collisions.</param>
    /// <param name="protectedToolNames">The tool names reserved by the host.</param>
    /// <returns>The external tool names that must not be published.</returns>
    IReadOnlySet<string> FindExternalToolCollisions(
        IReadOnlyList<PreparedCatalogPlugin> plugins,
        IReadOnlySet<string> protectedToolNames);
}
