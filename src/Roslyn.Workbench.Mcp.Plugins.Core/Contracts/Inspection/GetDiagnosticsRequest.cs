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
    [Description("The optional scope selector.")]
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the included diagnostic severities.
    /// </summary>
    [Description("The included diagnostic severities.")]
    public IReadOnlyList<string>? Severities { get; init; }

    /// <summary>
    /// Gets the included diagnostic identifiers.
    /// </summary>
    [Description("The included diagnostic identifiers.")]
    public IReadOnlyList<string>? Ids { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDiagnosticsMaxResults)]
    public int? DiagnosticsLimit { get; init; } = _defaultDiagnosticsMaxResults;

    /// <summary>
    /// Gets the effective diagnostics limit.
    /// </summary>
    internal int EffectiveDiagnosticsLimit => ResultLimit.GetEffectiveValue(DiagnosticsLimit, _defaultDiagnosticsMaxResults);
}
