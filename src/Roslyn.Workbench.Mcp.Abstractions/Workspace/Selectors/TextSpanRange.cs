using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a zero-based UTF-16 text span in a request or response model.
/// </summary>
public sealed record TextSpanRange
{
    /// <summary>
    /// Gets the zero-based UTF-16 start position.
    /// </summary>
    [Description("The zero-based UTF-16 start position.")]
    [Range(0, int.MaxValue)]
    public int Start { get; init; }

    /// <summary>
    /// Gets the zero-based UTF-16 length.
    /// </summary>
    [Description("The zero-based UTF-16 length.")]
    [Range(0, int.MaxValue)]
    public int Length { get; init; }
}
