namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Identifies the externally published response shape for a tool.
/// </summary>
public enum ToolResponseShapeKind
{
    Direct,
    Singleton,
    Collection,
    Mutation,
    CodeActionList,
}
