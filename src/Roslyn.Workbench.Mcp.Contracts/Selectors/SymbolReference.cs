namespace Roslyn.Workbench.Mcp.Contracts.Selectors;

/// <summary>
/// Represents a resolved symbol reference.
/// </summary>
public sealed record SymbolReference
{
    /// <summary>
    /// Gets the display name of the symbol.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the symbol kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the documentation comment identifier, when available.
    /// </summary>
    public string? DocumentationCommentId { get; init; }

    /// <summary>
    /// Gets the optional source location of the symbol.
    /// </summary>
    public ResolvedLocation? Location { get; init; }
}
