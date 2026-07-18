namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve detailed symbol information.
/// </summary>
public sealed record GetSymbolInfoRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether member summaries should be included.
    /// </summary>
    public bool IncludeMembers { get; init; }

    /// <summary>
    /// Gets a value indicating whether XML documentation should be included.
    /// </summary>
    public bool IncludeDocumentation { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
