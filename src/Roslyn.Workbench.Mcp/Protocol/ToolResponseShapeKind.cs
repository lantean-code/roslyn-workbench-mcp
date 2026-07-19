namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Identifies the externally published response shape for a tool.
/// </summary>
internal enum ToolResponseShapeKind
{
    Direct,
    Singleton,
    Collection,
    Mutation,
    CodeActionList,
}
