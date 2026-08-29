namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a resolved document reference.
/// </summary>
public sealed record DocumentReference
{
    /// <summary>
    /// Gets the document identifier.
    /// </summary>
    [Description("The document identifier.")]
    public string DocumentId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the normalized document path.
    /// </summary>
    [Description("The normalized document path.")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the owning project identifier.
    /// </summary>
    [Description("The owning project identifier.")]
    public string ProjectId { get; init; } = string.Empty;
}
