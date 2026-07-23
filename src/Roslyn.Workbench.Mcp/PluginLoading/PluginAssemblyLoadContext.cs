using System.Reflection;
using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private const string _codeAnalysisAssemblyPrefix = "Microsoft.CodeAnalysis";
    private const string _compositionAssemblyPrefix = "System.Composition";
    private const string _pluginsAssemblyName = "Roslyn.Workbench.Mcp.Plugins";
    private const string _workspaceAssemblyName = "Roslyn.Workbench.Mcp.Workspace";

    private readonly string _packageDirectory;
    private readonly IPluginPackagePathPolicy _packagePathPolicy;
    private readonly AssemblyDependencyResolver _resolver;

    public PluginAssemblyLoadContext(
        string packageDirectory,
        string entryAssemblyPath,
        IPluginPackagePathPolicy packagePathPolicy)
        : base($"RoslynWorkbenchPlugin:{Path.GetFileNameWithoutExtension(entryAssemblyPath)}", false)
    {
        _packageDirectory = packageDirectory;
        _packagePathPolicy = packagePathPolicy;
        _resolver = new AssemblyDependencyResolver(entryAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsSharedAssembly(assemblyName.Name))
        {
            return ResolveFromDefaultContext(assemblyName);
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is null)
        {
            return null;
        }

        return LoadFromAssemblyPath(GetContainedDependencyPath(assemblyPath));
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is null
            ? 0
            : LoadUnmanagedDllFromPath(GetContainedDependencyPath(libraryPath));
    }

    private static bool IsSharedAssembly(string? assemblyName)
    {
        return string.Equals(assemblyName, _pluginsAssemblyName, StringComparison.Ordinal)
            || string.Equals(assemblyName, _workspaceAssemblyName, StringComparison.Ordinal)
            || assemblyName?.StartsWith(_codeAnalysisAssemblyPrefix, StringComparison.Ordinal) == true
            || assemblyName?.StartsWith(_compositionAssemblyPrefix, StringComparison.Ordinal) == true;
    }

    private static Assembly ResolveFromDefaultContext(AssemblyName assemblyName)
    {
        var loadedAssembly = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName.Name, StringComparison.Ordinal));

        return loadedAssembly ?? AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
    }

    private string GetContainedDependencyPath(string dependencyPath)
    {
        if (_packagePathPolicy.TryGetContainedPath(_packageDirectory, dependencyPath, out var containedDependencyPath))
        {
            return containedDependencyPath;
        }

        throw new FileLoadException("Plugin dependency resolves outside its package directory.");
    }
}
