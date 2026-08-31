using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Creates isolated assembly-load contexts for external plugin packages.
/// </summary>
internal interface IPluginLoadContextFactory
{
    /// <summary>
    /// Attempts to create a load context rooted at a validated plugin entry assembly.
    /// </summary>
    /// <param name="packageDirectory">The directory containing the plugin package.</param>
    /// <param name="entryAssemblyPath">The path of the plugin entry assembly that anchors dependency resolution.</param>
    /// <param name="loadContext">The collectible load context created for the plugin package.</param>
    /// <returns><see langword="true"/> when a load context was created; otherwise, <see langword="false"/>.</returns>
    bool TryCreate(
        string packageDirectory,
        string entryAssemblyPath,
        [NotNullWhen(true)] out AssemblyLoadContext? loadContext);
}
