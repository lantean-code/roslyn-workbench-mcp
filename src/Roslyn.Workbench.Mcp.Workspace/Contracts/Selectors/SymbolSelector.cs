namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

/// <summary>
/// Represents a symbol selector backed by a source location or documentation comment identifier.
/// </summary>
public sealed record SymbolSelector
{
    /// <summary>
    /// Gets the source location selector.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the documentation comment identifier.
    /// </summary>
    public string? DocumentationCommentId { get; init; }
}
