namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a zero-based UTF-16 text span in a response model.
/// </summary>
public sealed record TextSpanRange
{
    /// <summary>
    /// Gets the zero-based UTF-16 start position.
    /// </summary>
    public int Start { get; init; }

    /// <summary>
    /// Gets the zero-based UTF-16 length.
    /// </summary>
    public int Length { get; init; }
}
