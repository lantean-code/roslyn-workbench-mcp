namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Represents shared or exclusive ownership of a Workspace operation gate until disposal.
/// </summary>
internal interface IWorkspaceOperationLease : IDisposable
{
}
