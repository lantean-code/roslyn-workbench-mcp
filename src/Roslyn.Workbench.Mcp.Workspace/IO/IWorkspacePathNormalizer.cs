namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal interface IWorkspacePathNormalizer
{
    bool TryGetFullPath(string path, out string fullPath);

    bool TryGetFullPath(string path, string basePath, out string fullPath);

    bool TryGetWorkspaceRelativePath(string workspaceRoot, string path, out string relativePath);
}
