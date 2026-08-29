namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one duplicate-code occurrence.
/// </summary>
internal sealed record DuplicateCodeOccurrence
{
    /// <summary>
    /// Gets the enclosing symbol.
    /// </summary>
    [Description("The enclosing symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the occurrence location.
    /// </summary>
    [Description("The occurrence location.")]
    public ResolvedLocation? Location { get; init; }

}
