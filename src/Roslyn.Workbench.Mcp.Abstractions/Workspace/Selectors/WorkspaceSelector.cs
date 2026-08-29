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
    [Description("Server-generated identifier of the target workspace; at least one workspace selector field is required.")]
    [NonEmptyGuid]
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Gets the optional caller-friendly workspace alias.
    /// </summary>
    [Description("Caller-friendly alias of the target workspace; at least one workspace selector field is required.")]
    public string? Alias { get; init; }

    /// <summary>
    /// Gets the absolute workspace path.
    /// </summary>
    [Description("Absolute path of the target workspace; at least one workspace selector field is required.")]
    public string? Path { get; init; }
}
