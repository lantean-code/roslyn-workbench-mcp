namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal interface IWorkspacePathComparison
{
    StringComparison Comparison { get; }

    StringComparer Comparer { get; }

    StringComparison GetComparison(string path);

    StringComparer GetComparer(string path);

    bool IsWindowsFileSystemPath(string path);
}
