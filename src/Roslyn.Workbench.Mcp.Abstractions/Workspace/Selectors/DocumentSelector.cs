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
    [Description("Project used to disambiguate the document, when needed.")]
    public ProjectSelector? Project { get; init; }

    /// <summary>
    /// Gets the normalized workspace-relative document path.
    /// </summary>
    [Description("Workspace-relative document path; provide either path or documentId, not both.")]
    public string? Path { get; init; }

    /// <summary>
    /// Gets the workspace-local document identifier.
    /// </summary>
    [Description("Workspace-local document identifier; provide either documentId or path, not both.")]
    public string? DocumentId { get; init; }
}
