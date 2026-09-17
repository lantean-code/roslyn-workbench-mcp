namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Retains stable compiler-error identity and bounded source-location data without retaining a compilation.
/// </summary>
internal sealed record CachedCompilerDiagnostic
{
    /// <summary>
    /// Gets the stable diagnostic identity used for multiset comparison.
    /// </summary>
    public required CompilerDiagnosticIdentity Identity { get; init; }

    /// <summary>
    /// Gets the source document identifier when the diagnostic belongs to a loaded document.
    /// </summary>
    public DocumentId? DocumentId { get; init; }

    /// <summary>
    /// Gets the display-only source span when present.
    /// </summary>
    public TextSpan? SourceSpan { get; init; }
}
