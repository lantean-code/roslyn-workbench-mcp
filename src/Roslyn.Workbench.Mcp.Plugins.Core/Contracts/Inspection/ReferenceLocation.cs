namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one reference search result.
/// </summary>
internal sealed record ReferenceLocation
{
    /// <summary>
    /// Gets the reference location.
    /// </summary>
    [Description("The reference location.")]
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the containing symbol, when available.
    /// </summary>
    [Description("The containing symbol, when available.")]
    public SymbolReference? ContainingSymbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether the location is a definition.
    /// </summary>
    [Description("Whether the location is a definition.")]
    public bool IsDefinition { get; init; }

    /// <summary>
    /// Gets a value indicating whether the location represents a write access.
    /// </summary>
    [Description("Whether the location represents a write access.")]
    public bool IsWrite { get; init; }

    /// <summary>
    /// Gets the optional source snippet.
    /// </summary>
    [Description("The optional source snippet.")]
    public string? Context { get; init; }
}
