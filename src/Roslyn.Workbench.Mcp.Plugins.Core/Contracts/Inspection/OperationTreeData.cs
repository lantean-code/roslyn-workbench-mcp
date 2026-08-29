namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-operation-tree.
/// </summary>
internal sealed record OperationTreeData : IQueryResponse
{
    /// <summary>
    /// Gets the projected root operation.
    /// </summary>
    [Description("The projected root operation.")]
    public OperationNode? Root { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tree was truncated.
    /// </summary>
    [Description("Whether the tree was truncated.")]
    public bool Truncated { get; init; }
}
