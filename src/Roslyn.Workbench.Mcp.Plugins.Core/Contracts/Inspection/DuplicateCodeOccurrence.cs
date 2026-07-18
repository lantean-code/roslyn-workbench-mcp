namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one duplicate-code occurrence.
/// </summary>
public sealed record DuplicateCodeOccurrence
{
    /// <summary>
    /// Gets the enclosing symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the occurrence location.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the compact source context.
    /// </summary>
    public string Context { get; init; } = string.Empty;
}
