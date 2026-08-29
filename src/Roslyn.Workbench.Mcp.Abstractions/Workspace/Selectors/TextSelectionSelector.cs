namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents an agent-friendly text selection selector.
/// </summary>
public sealed record TextSelectionSelector
{
    /// <summary>
    /// Gets the selected document.
    /// </summary>
    [Description("The selected document.")]
    public required DocumentSelector Document { get; init; }

    /// <summary>
    /// Gets the copied selected text.
    /// </summary>
    [Description("The copied selected text.")]
    public string SelectedText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional leading context.
    /// </summary>
    [Description("The optional leading context.")]
    public string? ContextBefore { get; init; }

    /// <summary>
    /// Gets the optional trailing context.
    /// </summary>
    [Description("The optional trailing context.")]
    public string? ContextAfter { get; init; }
}
