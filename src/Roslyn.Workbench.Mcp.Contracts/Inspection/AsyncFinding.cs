using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents one async analysis finding.
/// </summary>
public sealed record AsyncFinding
{
    /// <summary>
    /// Gets the finding kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the associated symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the finding location.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the finding message.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
