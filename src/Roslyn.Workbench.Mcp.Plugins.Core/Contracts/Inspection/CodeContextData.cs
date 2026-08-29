namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-code-context.
/// </summary>
internal sealed record CodeContextData : IQueryResponse
{
    /// <summary>
    /// Gets the resolved source location.
    /// </summary>
    [Description("The resolved source location.")]
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the bounded source text.
    /// </summary>
    [Description("The bounded source text.")]
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets the enclosing symbol chain from the innermost symbol outward.
    /// </summary>
    [Description("The enclosing symbol chain from the innermost symbol outward.")]
    public BoundedCollection<SymbolReference> EnclosingSymbols { get; init; } = BoundedCollection.Empty<SymbolReference>();

    /// <summary>
    /// Gets the diagnostics projected for the selected location.
    /// </summary>
    [Description("The diagnostics projected for the selected location.")]
    public BoundedCollection<DiagnosticInfo> Diagnostics { get; init; } = BoundedCollection.Empty<DiagnosticInfo>();
}
