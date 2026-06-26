namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Represents the host-owned execution context for a query tool.
/// </summary>
public interface IQueryContext : IToolExecutionContext
{
    /// <summary>
    /// Gets the maximum serialized response size, in bytes, for this invocation.
    /// </summary>
    int MaxResponseBytes { get; }
}
