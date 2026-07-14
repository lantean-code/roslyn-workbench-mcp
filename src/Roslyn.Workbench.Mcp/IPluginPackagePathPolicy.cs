namespace Roslyn.Workbench.Mcp;

internal interface IPluginPackagePathPolicy
{
    StringComparer Comparer { get; }

    bool TryGetContainedPath(string packageDirectory, string candidatePath, out string containedPath);
}
