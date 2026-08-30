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
    public required string DocumentId { get; init; }

    /// <summary>
    /// Gets the normalized document path.
    /// </summary>
    [Description("The normalized document path.")]
    public required string Path { get; init; }

    /// <summary>
    /// Gets the owning project identifier.
    /// </summary>
    [Description("The owning project identifier.")]
    public required string ProjectId { get; init; }
}
