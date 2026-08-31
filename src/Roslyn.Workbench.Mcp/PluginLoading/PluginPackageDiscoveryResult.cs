namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Reports the accepted candidate or discovery failure for one package directory or search root.
/// </summary>
internal sealed record PluginPackageDiscoveryResult
{
    /// <summary>
    /// Gets the directory name used to identify a rejected candidate before plugin metadata is available.
    /// </summary>
    public string FallbackIdentity { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether discovery produced a plugin candidate.
    /// </summary>
    public PluginPackageCandidate? Candidate { get; init; }

    /// <summary>
    /// Gets the reason the package or search root was rejected.
    /// </summary>
    public string? Error { get; init; }
}
