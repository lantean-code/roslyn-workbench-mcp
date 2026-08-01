namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a document selector for workspace-local document resolution.
/// </summary>
[RequiresExactlyOne(
    nameof(Path),
    nameof(DocumentId),
    ErrorMessage = "DocumentSelector must provide exactly one of Path or DocumentId.")]
public sealed record DocumentSelector
{
    /// <summary>
    /// Gets the optional project scope used to disambiguate the document.
    /// </summary>
    public ProjectSelector? Project { get; init; }

    /// <summary>
    /// Gets the normalized workspace-relative document path.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets the workspace-local document identifier.
    /// </summary>
    public string? DocumentId { get; init; }
}
