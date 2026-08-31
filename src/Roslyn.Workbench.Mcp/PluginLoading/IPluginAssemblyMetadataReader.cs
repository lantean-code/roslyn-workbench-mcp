namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Reads plugin entry-point metadata without loading the assembly into the host.
/// </summary>
internal interface IPluginAssemblyMetadataReader
{
    /// <summary>
    /// Inspects the plugin assembly metadata.
    /// </summary>
    /// <param name="assemblyPath">The path of the plugin assembly whose metadata is read.</param>
    /// <returns>The accepted entry-point metadata or the reason inspection rejected the assembly.</returns>
    PluginAssemblyInspectionResult Inspect(string assemblyPath);
}
