namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-code-context.
/// </summary>
internal sealed record CodeContextData : IQueryResponse
{
    /// <summary>
    /// Gets the resolved source location.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the bounded source text.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets the enclosing symbol chain from the innermost symbol outward.
    /// </summary>
    public BoundedCollection<SymbolReference> EnclosingSymbols { get; init; } = BoundedCollection.Empty<SymbolReference>();

    /// <summary>
    /// Gets the diagnostics projected for the selected location.
    /// </summary>
    public BoundedCollection<DiagnosticInfo> Diagnostics { get; init; } = BoundedCollection.Empty<DiagnosticInfo>();
}
