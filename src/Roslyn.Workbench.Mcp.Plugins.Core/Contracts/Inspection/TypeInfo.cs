namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a projected Roslyn type.
/// </summary>
internal sealed record TypeInfo
{
    /// <summary>
    /// Gets the display name.
    /// </summary>
    [Description("The display name.")]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the Roslyn type kind.
    /// </summary>
    [Description("The Roslyn type kind.")]
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the nullable annotation.
    /// </summary>
    [Description("The nullable annotation.")]
    public string? NullableAnnotation { get; init; }

    /// <summary>
    /// Gets the documentation-comment identifier, when available.
    /// </summary>
    [Description("The documentation-comment identifier, when available.")]
    public string? DocumentationCommentId { get; init; }
}
