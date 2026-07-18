namespace Roslyn.Workbench.Mcp.Workspace.Selection;

internal interface IWorkspaceSelector
{
    WorkspaceSelectionResult Select(WorkspaceHostSnapshot hostSnapshot, WorkspaceSelector? selector);
}
