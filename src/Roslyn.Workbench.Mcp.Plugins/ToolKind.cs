namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Identifies whether a registered plugin tool is a query or a mutation.
/// </summary>
public enum ToolKind
{
    /// <summary>
    /// The tool is read-only.
    /// </summary>
    Query,

    /// <summary>
    /// The tool can stage a change.
    /// </summary>
    Mutation,
}
