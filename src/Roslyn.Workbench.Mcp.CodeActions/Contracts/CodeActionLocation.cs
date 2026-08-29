using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Identifies the precise document location to which a listed code action applies.
/// </summary>
internal sealed record CodeActionLocation
{
    /// <summary>
    /// Gets the project-aware source document identity.
    /// </summary>
    [Description("The project-aware source document identity.")]
    public required DocumentReference Document { get; init; }

    /// <summary>
    /// Gets the zero-based UTF-16 source span.
    /// </summary>
    [Description("The zero-based UTF-16 source span.")]
    public required TextSpanRange Span { get; init; }

    /// <summary>
    /// Gets the zero-based source line.
    /// </summary>
    [Description("The zero-based source line.")]
    public int Line { get; init; }

    /// <summary>
    /// Gets the zero-based source column.
    /// </summary>
    [Description("The zero-based source column.")]
    public int Column { get; init; }
}
