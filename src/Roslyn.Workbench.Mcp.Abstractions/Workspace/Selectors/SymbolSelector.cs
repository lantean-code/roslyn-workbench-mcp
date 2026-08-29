namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a symbol selector backed by a source location or documentation comment identifier,
/// optionally constrained to a project.
/// </summary>
[RequiresExactlyOne(
    nameof(Location),
    nameof(DocumentationCommentId),
    ErrorMessage = "SymbolSelector must provide exactly one of Location or DocumentationCommentId.")]
public sealed record SymbolSelector
{
    /// <summary>
    /// Gets the optional project scope used to disambiguate the symbol.
    /// </summary>
    [Description("Project used to disambiguate the symbol, when needed.")]
    public ProjectSelector? Project { get; init; }

    /// <summary>
    /// Gets the source location selector.
    /// </summary>
    [Description("Source location that identifies the symbol; provide either location or documentationCommentId, not both.")]
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the documentation comment identifier.
    /// </summary>
    [Description("Documentation-comment identifier that identifies the symbol; provide either documentationCommentId or location, not both.")]
    public string? DocumentationCommentId { get; init; }
}
