namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Represents the host-owned execution context for a query tool.
/// </summary>
public interface IQueryContext :
    IToolExecutionContext
{
    /// <summary>
    /// Gets the result cache bound to this query invocation, Workspace snapshot, plugin and tool.
    /// </summary>
    IQueryResultCache QueryResultCache { get; }
}
