namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a resolved document reference.
/// </summary>
public sealed record DocumentReference
{
    /// <summary>
    /// Gets the document identifier.
    /// </summary>
    public string DocumentId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the normalized document path.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the owning project identifier.
    /// </summary>
    public string ProjectId { get; init; } = string.Empty;
}
