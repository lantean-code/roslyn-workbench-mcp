namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve diagnostics for a scope.
/// </summary>
internal sealed record GetDiagnosticsRequest : WorkspaceBoundRequest
{
    private const int _defaultDiagnosticsMaxResults = 200;

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
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDiagnosticsMaxResults)]
    public int? DiagnosticsLimit { get; init; } = _defaultDiagnosticsMaxResults;

    internal int EffectiveDiagnosticsLimit => ResultLimit.GetEffectiveValue(DiagnosticsLimit, _defaultDiagnosticsMaxResults);
}
