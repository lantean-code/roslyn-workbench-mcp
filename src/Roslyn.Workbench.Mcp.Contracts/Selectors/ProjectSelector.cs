namespace Roslyn.Workbench.Mcp.Contracts.Selectors;

/// <summary>
/// Represents a project selector for workspace-local project resolution.
/// </summary>
public sealed record ProjectSelector
{
    /// <summary>
    /// Gets the workspace-local project identifier.
    /// </summary>
    public string? ProjectId { get; init; }

    /// <summary>
    /// Gets the project name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the normalized workspace-relative project path.
    /// </summary>
    public string? Path { get; init; }
}
