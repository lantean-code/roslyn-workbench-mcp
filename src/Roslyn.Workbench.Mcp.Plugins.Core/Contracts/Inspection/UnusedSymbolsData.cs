using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-unused-symbols.
/// </summary>
public sealed record UnusedSymbolsData
{
    /// <summary>
    /// Gets the returned unused symbol candidates.
    /// </summary>
    public BoundedCollection<UnusedSymbolCandidate> Candidates { get; init; } = BoundedCollection<UnusedSymbolCandidate>.Empty();
}
