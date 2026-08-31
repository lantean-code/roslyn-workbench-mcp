namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Identifies the single entry assembly and metadata accepted from an external plugin package.
/// </summary>
internal sealed record PluginPackageCandidate
{
    /// <summary>
    /// Gets the canonical directory that bounds all plugin dependencies.
    /// </summary>
    public string PackageDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets the canonical path of the assembly carrying the plugin entry point.
    /// </summary>
    public string EntryAssemblyPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the identity and compatibility metadata read from the entry point.
    /// </summary>
    public PluginEntryPointMetadata EntryPoint { get; init; } = new();
}
