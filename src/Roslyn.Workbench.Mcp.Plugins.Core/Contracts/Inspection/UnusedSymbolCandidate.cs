namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one unused symbol candidate.
/// </summary>
internal sealed record UnusedSymbolCandidate
{
    /// <summary>
    /// Gets the candidate symbol.
    /// </summary>
    [Description("The candidate symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the source location for the candidate.
    /// </summary>
    [Description("The source location for the candidate.")]
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the confidence label for the candidate.
    /// </summary>
    [Description("The confidence label for the candidate.")]
    public required string Confidence { get; init; }

    /// <summary>
    /// Gets the reasons the symbol was reported.
    /// </summary>
    [Description("The reasons the symbol was reported.")]
    public IReadOnlyList<string> Reasons { get; init; } = [];
}
