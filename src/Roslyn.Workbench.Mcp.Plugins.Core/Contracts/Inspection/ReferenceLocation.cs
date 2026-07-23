namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one reference search result.
/// </summary>
internal sealed record ReferenceLocation
{
    /// <summary>
    /// Gets the reference location.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the containing symbol, when available.
    /// </summary>
    public SymbolReference? ContainingSymbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether the location is a definition.
    /// </summary>
    public bool IsDefinition { get; init; }

    /// <summary>
    /// Gets a value indicating whether the location represents a write access.
    /// </summary>
    public bool IsWrite { get; init; }

    /// <summary>
    /// Gets the optional source snippet.
    /// </summary>
    public string? Context { get; init; }
}
