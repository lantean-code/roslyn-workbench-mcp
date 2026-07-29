namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to return a bounded code window and enclosing semantic context.
/// </summary>
internal sealed record GetCodeContextRequest : WorkspaceBoundRequest
{
    private const int _defaultAfterLines = 10;
    private const int _defaultBeforeLines = 10;

    /// <summary>
    /// Gets the location selector.
    /// </summary>
    public required LocationSelector Location { get; init; }

    /// <summary>
    /// Gets the number of lines to include before the selected location.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultBeforeLines)]
    public int BeforeLines { get; init; } = _defaultBeforeLines;

    /// <summary>
    /// Gets the number of lines to include after the selected location.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultAfterLines)]
    public int AfterLines { get; init; } = _defaultAfterLines;

    /// <summary>
    /// Gets a value indicating whether the enclosing symbol chain should be included.
    /// </summary>
    public bool IncludeEnclosingSymbols { get; init; }

    /// <summary>
    /// Gets a value indicating whether diagnostics should be included for the selected span.
    /// </summary>
    public bool IncludeDiagnostics { get; init; }

    /// <summary>
    /// Gets the expected workspace snapshot.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
