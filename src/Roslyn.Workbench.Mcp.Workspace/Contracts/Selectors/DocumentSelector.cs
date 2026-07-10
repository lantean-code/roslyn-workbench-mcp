namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

/// <summary>
/// Represents a document selector for workspace-local document resolution.
/// </summary>
public sealed record DocumentSelector
{
    /// <summary>
    /// Gets the normalized workspace-relative document path.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets the workspace-local document identifier.
    /// </summary>
    public string? DocumentId { get; init; }
}
