using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

/// <summary>
/// Creates the host-owned execution contexts used by plugin tools.
/// </summary>
internal interface IWorkspaceExecutionContextFactory : IToolExecutionContextFactory
{ }
