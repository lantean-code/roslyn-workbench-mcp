namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

/// <summary>
/// Represents a symbol selector backed by a source location or documentation comment identifier,
/// optionally constrained to a project.
/// </summary>
public sealed record SymbolSelector
{
    /// <summary>
    /// Gets the optional project scope used to disambiguate the symbol.
    /// </summary>
    public ProjectSelector? Project { get; init; }

    /// <summary>
    /// Gets the source location selector.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the documentation comment identifier.
    /// </summary>
    public string? DocumentationCommentId { get; init; }
}
