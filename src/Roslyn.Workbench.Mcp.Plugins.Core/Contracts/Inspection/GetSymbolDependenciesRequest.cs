using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to return the direct dependencies of a symbol.
/// </summary>
public sealed record GetSymbolDependenciesRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether containing assembly names should be included with each dependency.
    /// </summary>
    public bool IncludeAssemblies { get; init; } = true;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public CollectionLimit? DependenciesLimit { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
