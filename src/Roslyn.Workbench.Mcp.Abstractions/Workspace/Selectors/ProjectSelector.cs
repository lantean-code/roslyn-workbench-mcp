namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a project selector for workspace-local project resolution.
/// </summary>
[RequiresAtLeastOne(
    nameof(ProjectId),
    nameof(Name),
    nameof(Path),
    nameof(TargetFramework),
    ErrorMessage = "ProjectSelector must provide at least one of ProjectId, Name, Path, or TargetFramework.")]
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

    /// <summary>
    /// Gets the target framework used to select a target-specific Roslyn project.
    /// </summary>
    public string? TargetFramework { get; init; }
}
