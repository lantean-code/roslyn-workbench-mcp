namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal interface IWorkspacePathComparison
{
    StringComparison GetComparison(string path);

    FileSystemPathKey CreateKey(string path);

    bool IsWindowsFileSystemPath(string path);
}
