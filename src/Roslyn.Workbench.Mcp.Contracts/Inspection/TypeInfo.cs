namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a projected Roslyn type.
/// </summary>
public sealed record TypeInfo
{
    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Roslyn type kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the nullable annotation.
    /// </summary>
    public string? NullableAnnotation { get; init; }

    /// <summary>
    /// Gets the documentation-comment identifier, when available.
    /// </summary>
    public string? DocumentationCommentId { get; init; }
}
