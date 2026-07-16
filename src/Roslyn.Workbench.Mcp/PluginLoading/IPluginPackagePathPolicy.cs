namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginPackagePathPolicy
{
    StringComparer Comparer { get; }

    bool TryGetContainedPath(string packageDirectory, string candidatePath, out string containedPath);
}
