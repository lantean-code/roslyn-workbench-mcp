namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to return a bounded code window and enclosing semantic context.
/// </summary>
internal sealed record GetCodeContextRequest : WorkspaceBoundRequest
{
    private const int _defaultAfterLines = 10;
    private const int _defaultBeforeLines = 10;
    private const int _defaultDiagnosticsMaxResults = 50;
    private const int _defaultEnclosingSymbolsMaxResults = 16;
    private const int _maximumContextLines = 100;

    /// <summary>
    /// Gets the location selector.
    /// </summary>
    [Description("The location selector.")]
    public required LocationSelector Location { get; init; }

    /// <summary>
    /// Gets the optional number of lines to include before the selected location.
    /// </summary>
    [Description("The optional number of lines to include before the selected location.")]
    [Range(0, _maximumContextLines)]
    [DefaultValue(_defaultBeforeLines)]
    public int? BeforeLines { get; init; } = _defaultBeforeLines;

    /// <summary>
    /// Gets the optional number of lines to include after the selected location.
    /// </summary>
    [Description("The optional number of lines to include after the selected location.")]
    [Range(0, _maximumContextLines)]
    [DefaultValue(_defaultAfterLines)]
    public int? AfterLines { get; init; } = _defaultAfterLines;

    /// <summary>
    /// Gets a value indicating whether the enclosing symbol chain should be included.
    /// </summary>
    [Description("Whether the enclosing symbol chain should be included.")]
    public bool IncludeEnclosingSymbols { get; init; }

    /// <summary>
    /// Gets a value indicating whether diagnostics should be included for the selected span.
    /// </summary>
    [Description("Whether diagnostics should be included for the selected span.")]
    public bool IncludeDiagnostics { get; init; }

    /// <summary>
    /// Gets the optional enclosing symbols limit.
    /// </summary>
    [Description("Maximum number of enclosing symbols to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultEnclosingSymbolsMaxResults)]
    public int? EnclosingSymbolsLimit { get; init; } = _defaultEnclosingSymbolsMaxResults;

    /// <summary>
    /// Gets the optional diagnostics limit.
    /// </summary>
    [Description("Maximum number of diagnostics to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDiagnosticsMaxResults)]
    public int? DiagnosticsLimit { get; init; } = _defaultDiagnosticsMaxResults;

    /// <summary>
    /// Gets the expected workspace snapshot.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective before lines.
    /// </summary>
    internal int EffectiveBeforeLines => ResultLimit.GetEffectiveValue(BeforeLines, _defaultBeforeLines);

    /// <summary>
    /// Gets the effective after lines.
    /// </summary>
    internal int EffectiveAfterLines => ResultLimit.GetEffectiveValue(AfterLines, _defaultAfterLines);

    /// <summary>
    /// Gets the effective enclosing symbols limit.
    /// </summary>
    internal int EffectiveEnclosingSymbolsLimit => ResultLimit.GetEffectiveValue(EnclosingSymbolsLimit, _defaultEnclosingSymbolsMaxResults);

    /// <summary>
    /// Gets the effective diagnostics limit.
    /// </summary>
    internal int EffectiveDiagnosticsLimit => ResultLimit.GetEffectiveValue(DiagnosticsLimit, _defaultDiagnosticsMaxResults);
}
