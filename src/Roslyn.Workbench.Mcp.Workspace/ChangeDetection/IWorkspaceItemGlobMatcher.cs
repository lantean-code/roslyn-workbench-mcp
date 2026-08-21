namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceItemGlobMatcher
{
    bool Matches(string path);
}
