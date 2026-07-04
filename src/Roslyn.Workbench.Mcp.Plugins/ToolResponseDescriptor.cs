namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Describes the externally published response shape for a tool.
/// </summary>
public sealed record ToolResponseDescriptor
{
    /// <summary>
    /// Gets the response shape kind.
    /// </summary>
    public ToolResponseShapeKind Kind { get; init; }

    /// <summary>
    /// Gets the CLR collection property name when the response shape is collection-based.
    /// </summary>
    public string? CollectionPropertyName { get; init; }
}
