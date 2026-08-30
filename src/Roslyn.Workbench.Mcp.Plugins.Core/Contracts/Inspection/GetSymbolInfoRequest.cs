namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve detailed symbol information.
/// </summary>
internal sealed record GetSymbolInfoRequest : WorkspaceBoundRequest
{
    private const int _defaultDeclarationsMaxResults = 32;
    private const int _defaultParametersMaxResults = 64;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    [Description("The symbol selector.")]
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether XML documentation should be included.
    /// </summary>
    [Description("Whether XML documentation should be included.")]
    public bool IncludeDocumentation { get; init; }

    /// <summary>
    /// Gets the optional parameters limit.
    /// </summary>
    [Description("Maximum number of parameters to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultParametersMaxResults)]
    public int? ParametersLimit { get; init; } = _defaultParametersMaxResults;

    /// <summary>
    /// Gets the optional declarations limit.
    /// </summary>
    [Description("Maximum number of declarations to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDeclarationsMaxResults)]
    public int? DeclarationsLimit { get; init; } = _defaultDeclarationsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveParametersLimit => ResultLimit.GetEffectiveValue(ParametersLimit, _defaultParametersMaxResults);

    internal int EffectiveDeclarationsLimit => ResultLimit.GetEffectiveValue(DeclarationsLimit, _defaultDeclarationsMaxResults);
}
