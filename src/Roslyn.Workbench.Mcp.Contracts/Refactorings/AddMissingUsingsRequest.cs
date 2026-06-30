using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Requests addition of missing using directives across a selected scope.
/// </summary>
public sealed record AddMissingUsingsRequest
{
    /// <summary>
    /// Gets the scope to clean.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request prefers global using directives.
    /// </summary>
    public bool PreferGlobalUsings { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the staged mutation.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
