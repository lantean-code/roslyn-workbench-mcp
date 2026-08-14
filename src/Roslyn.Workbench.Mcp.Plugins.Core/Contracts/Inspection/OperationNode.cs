namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one node in an operation-tree projection.
/// </summary>
internal sealed record OperationNode
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
    /// Gets a value indicating whether the operation has a constant value.
    /// </summary>
    public bool HasConstantValue { get; init; }

    /// <summary>
    /// Gets the exact source location of the operation.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets a value indicating whether child nodes were truncated.
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// Gets the projected child operations.
    /// </summary>
    public IReadOnlyList<OperationNode> Children { get; init; } = [];
}
