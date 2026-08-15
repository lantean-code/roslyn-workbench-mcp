namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginPackagePathPolicy
{
    FileSystemPathKey CreateKey(string path);

    bool TryGetContainedPath(string packageDirectory, string candidatePath, out string containedPath);
}
