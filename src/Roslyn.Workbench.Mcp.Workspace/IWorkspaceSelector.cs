using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace;

internal interface IWorkspaceSelector
{
    WorkspaceSelectionResult Select(WorkspaceHostSnapshot hostSnapshot, WorkspaceSelector? selector);
}
