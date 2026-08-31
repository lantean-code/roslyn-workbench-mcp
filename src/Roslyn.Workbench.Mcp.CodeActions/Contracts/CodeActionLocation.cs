using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Identifies the precise document location to which a listed Code Action applies.
/// </summary>
internal sealed record CodeActionLocation
{
    /// <summary>
    /// The project-aware source document identity.
    /// </summary>
    [Description("The project-aware source document identity.")]
    public required DocumentReference Document { get; init; }

    /// <summary>
    /// The zero-based UTF-16 source span.
    /// </summary>
    [Description("The zero-based UTF-16 source span.")]
    public required TextSpanRange Span { get; init; }

    /// <summary>
    /// The zero-based source line.
    /// </summary>
    [Description("The zero-based source line.")]
    public int Line { get; init; }

    /// <summary>
    /// The zero-based source column.
    /// </summary>
    [Description("The zero-based source column.")]
    public int Column { get; init; }
}
