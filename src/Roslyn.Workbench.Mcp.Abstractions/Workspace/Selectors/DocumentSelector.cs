namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a document selector for workspace-local document resolution.
/// </summary>
[Description("Provide exactly one of path or documentId.")]
[RequiresExactlyOne(
    nameof(Path),
    nameof(DocumentId),
    ErrorMessage = "DocumentSelector must provide exactly one of Path or DocumentId.")]
public sealed record DocumentSelector
{
    /// <summary>
    /// Gets the optional project scope used to disambiguate the document.
    /// </summary>
    [Description("Disambiguates documents with matching paths or IDs.")]
    public ProjectSelector? Project { get; init; }

    /// <summary>
    /// Gets the normalized workspace-relative document path.
    /// </summary>
    [Description("Workspace-relative path.")]
    public string? Path { get; init; }

    /// <summary>
    /// Gets the workspace-local document identifier.
    /// </summary>
    [Description("Workspace-local document ID.")]
    public string? DocumentId { get; init; }
}
