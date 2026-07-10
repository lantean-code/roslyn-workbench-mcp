using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one unused symbol candidate.
/// </summary>
public sealed record UnusedSymbolCandidate
{
    /// <summary>
    /// Gets the candidate symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the source location for the candidate.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the confidence label for the candidate.
    /// </summary>
    public string Confidence { get; init; } = string.Empty;

    /// <summary>
    /// Gets the reasons the symbol was reported.
    /// </summary>
    public IReadOnlyList<string> Reasons { get; init; } = [];
}
