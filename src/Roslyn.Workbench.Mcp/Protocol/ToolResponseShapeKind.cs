namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Identifies the externally published response shape for a tool.
/// </summary>
internal enum ToolResponseShapeKind
{
    /// <summary>
    /// The response is published directly without a standard tool-result envelope.
    /// </summary>
    Direct,
    /// <summary>
    /// The query returns one optional data value.
    /// </summary>
    Singleton,
    /// <summary>
    /// The query returns a bounded collection of data values.
    /// </summary>
    Collection,
    /// <summary>
    /// The tool returns the result of a transactional mutation.
    /// </summary>
    Mutation,
    /// <summary>
    /// The tool lists available Code Actions.
    /// </summary>
    CodeActionList,
}
