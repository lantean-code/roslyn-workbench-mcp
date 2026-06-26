namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-operation-tree.
/// </summary>
public sealed record OperationTreeData
{
    /// <summary>
    /// Gets the projected root operation.
    /// </summary>
    public OperationNode? Root { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tree was truncated.
    /// </summary>
    public bool Truncated { get; init; }
}
