namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal interface IWorkspacePathComparison
{
    StringComparison Comparison { get; }

    StringComparer Comparer { get; }
}
