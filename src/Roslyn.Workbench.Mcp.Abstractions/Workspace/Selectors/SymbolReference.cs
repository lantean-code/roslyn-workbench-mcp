namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a resolved symbol reference.
/// </summary>
public sealed record SymbolReference
{
    /// <summary>
    /// Gets the display name of the symbol.
    /// </summary>
    [Description("The display name of the symbol.")]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the symbol kind.
    /// </summary>
    [Description("The symbol kind.")]
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the documentation comment identifier, when available.
    /// </summary>
    [Description("The documentation comment identifier, when available.")]
    public string? DocumentationCommentId { get; init; }

    /// <summary>
    /// Gets the optional source location of the symbol.
    /// </summary>
    [Description("The optional source location of the symbol.")]
    public ResolvedLocation? Location { get; init; }
}
