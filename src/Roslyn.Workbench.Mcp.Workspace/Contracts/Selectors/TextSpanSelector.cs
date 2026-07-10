namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

/// <summary>
/// Represents a document-bound text span selector.
/// </summary>
public sealed record TextSpanSelector
{
    /// <summary>
    /// Gets the selected document.
    /// </summary>
    public DocumentSelector? Document { get; init; }

    /// <summary>
    /// Gets the zero-based UTF-16 start position.
    /// </summary>
    public int Start { get; init; }

    /// <summary>
    /// Gets the zero-based UTF-16 length.
    /// </summary>
    public int Length { get; init; }
}
