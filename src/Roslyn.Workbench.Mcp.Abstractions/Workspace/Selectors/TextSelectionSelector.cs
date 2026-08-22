namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents an agent-friendly text selection selector.
/// </summary>
public sealed record TextSelectionSelector
{
    /// <summary>
    /// Gets the selected document.
    /// </summary>
    public required DocumentSelector Document { get; init; }

    /// <summary>
    /// Gets the copied selected text.
    /// </summary>
    public string SelectedText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional leading context.
    /// </summary>
    public string? ContextBefore { get; init; }

    /// <summary>
    /// Gets the optional trailing context.
    /// </summary>
    public string? ContextAfter { get; init; }
}
