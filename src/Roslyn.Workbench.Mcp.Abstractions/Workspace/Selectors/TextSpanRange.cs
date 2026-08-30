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
    [Description("Zero-based UTF-16 offset.")]
    [Range(0, int.MaxValue)]
    public int Start { get; init; }

    /// <summary>
    /// Gets the zero-based UTF-16 length.
    /// </summary>
    [Description("UTF-16 code-unit length.")]
    [Range(0, int.MaxValue)]
    public int Length { get; init; }
}
