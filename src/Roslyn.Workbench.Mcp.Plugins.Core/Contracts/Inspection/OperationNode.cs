namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one node in an operation-tree projection.
/// </summary>
internal sealed record OperationNode
{
    /// <summary>
    /// Gets the Roslyn operation kind.
    /// </summary>
    [Description("The Roslyn operation kind.")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the associated type display name, when available.
    /// </summary>
    [Description("The associated type display name, when available.")]
    public string? Type { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation has a constant value.
    /// </summary>
    [Description("Whether the operation has a constant value.")]
    public bool HasConstantValue { get; init; }

    /// <summary>
    /// Gets the exact source location of the operation.
    /// </summary>
    [Description("The exact source location of the operation.")]
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets a value indicating whether child nodes were truncated.
    /// </summary>
    [Description("Whether child nodes were truncated.")]
    public bool Truncated { get; init; }

    /// <summary>
    /// Gets the projected child operations.
    /// </summary>
    [Description("The projected child operations.")]
    public IReadOnlyList<OperationNode> Children { get; init; } = [];
}
