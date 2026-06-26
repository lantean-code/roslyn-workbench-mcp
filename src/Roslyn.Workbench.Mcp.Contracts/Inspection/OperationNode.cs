namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents one node in an operation-tree projection.
/// </summary>
public sealed record OperationNode
{
    /// <summary>
    /// Gets the Roslyn operation kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the associated type display name, when available.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Gets the constant value, when available.
    /// </summary>
    public string? ConstantValue { get; init; }

    /// <summary>
    /// Gets the source syntax for the operation.
    /// </summary>
    public string Syntax { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether child nodes were truncated.
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// Gets the projected child operations.
    /// </summary>
    public IReadOnlyList<OperationNode> Children { get; init; } = [];
}
