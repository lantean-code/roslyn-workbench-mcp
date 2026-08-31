using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Creates isolated plugin load contexts only for entry assemblies contained by their package.
/// </summary>
internal sealed class PluginLoadContextFactory : IPluginLoadContextFactory
{
    private readonly IPluginPackagePathPolicy _packagePathPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadContextFactory"/> class.
    /// </summary>
    /// <param name="packagePathPolicy">The policy that validates and canonicalises plugin package paths.</param>
    public PluginLoadContextFactory(IPluginPackagePathPolicy packagePathPolicy)
    {
        _packagePathPolicy = packagePathPolicy;
    }

    /// <summary>
    /// Attempts to create an isolated load context after verifying the entry assembly is contained by its package.
    /// </summary>
    /// <param name="packageDirectory">The directory containing the plugin package.</param>
    /// <param name="entryAssemblyPath">The path of the plugin entry assembly that anchors dependency resolution.</param>
    /// <param name="loadContext">The isolated load context created for the plugin package.</param>
    /// <returns><see langword="true"/> when the entry assembly is contained and a context was created; otherwise, <see langword="false"/>.</returns>
    public bool TryCreate(
        string packageDirectory,
        string entryAssemblyPath,
        [NotNullWhen(true)] out AssemblyLoadContext? loadContext)
    {
        if (!_packagePathPolicy.TryGetContainedPath(packageDirectory, entryAssemblyPath, out var containedEntryAssemblyPath))
        {
            loadContext = null;
            return false;
        }

        loadContext = new PluginAssemblyLoadContext(packageDirectory, containedEntryAssemblyPath, _packagePathPolicy);
        return true;
    }
}
