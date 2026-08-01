namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Identifies one loaded workspace for a routed tool invocation.
/// </summary>
[RequiresAtLeastOne(
    nameof(WorkspaceId),
    nameof(Alias),
    nameof(Path),
    ErrorMessage = "WorkspaceSelector must provide at least one of WorkspaceId, Alias, or Path.")]
public sealed record WorkspaceSelector
{
    /// <summary>
    /// Gets the server-generated workspace identifier.
    /// </summary>
    [NonEmptyGuid]
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Gets the optional caller-friendly workspace alias.
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    /// Gets the absolute workspace path.
    /// </summary>
    public string? Path { get; init; }
}
