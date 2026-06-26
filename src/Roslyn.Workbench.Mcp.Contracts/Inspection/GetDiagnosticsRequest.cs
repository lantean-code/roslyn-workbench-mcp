using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve diagnostics for a scope.
/// </summary>
public sealed record GetDiagnosticsRequest
{
    /// <summary>
    /// Gets the optional scope selector.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the included diagnostic severities.
    /// </summary>
    public IReadOnlyList<string>? Severities { get; init; }

    /// <summary>
    /// Gets the included diagnostic identifiers.
    /// </summary>
    public IReadOnlyList<string>? Ids { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public ResultLimit? Limit { get; init; }
}
