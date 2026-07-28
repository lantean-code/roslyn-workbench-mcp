namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Identifies the precise document location to which a listed code action applies.
/// </summary>
internal sealed record CodeActionLocation
{
    /// <summary>
    /// Gets the project-aware source document identity.
    /// </summary>
    public required DocumentReference Document { get; init; }

    /// <summary>
    /// Gets the zero-based UTF-16 source span.
    /// </summary>
    public required TextSpanRange Span { get; init; }

    /// <summary>
    /// Gets the zero-based source line.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Gets the zero-based source column.
    /// </summary>
    public int Column { get; init; }
}
