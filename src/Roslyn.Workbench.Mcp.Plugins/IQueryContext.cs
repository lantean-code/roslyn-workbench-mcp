using Roslyn.Workbench.Mcp.Plugins.CodeActions;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Represents the host-owned execution context for a query tool.
/// </summary>
public interface IQueryContext :
    IToolExecutionContext,
    ICodeActionQueryWorkflow
{
}
