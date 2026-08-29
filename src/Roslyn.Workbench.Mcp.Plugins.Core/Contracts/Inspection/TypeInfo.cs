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
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Roslyn type kind.
    /// </summary>
    [Description("The Roslyn type kind.")]
    public string Kind { get; init; } = string.Empty;

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
