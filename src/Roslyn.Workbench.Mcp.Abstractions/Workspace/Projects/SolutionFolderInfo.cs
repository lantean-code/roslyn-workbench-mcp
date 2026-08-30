namespace Roslyn.Workbench.Mcp.Workspace.Projects;

/// <summary>
/// Represents one solution folder and its hierarchy position.
/// </summary>
public sealed record SolutionFolderInfo
{
    /// <summary>
    /// Gets the folder display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the canonical folder path within the solution hierarchy.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the parent folder path, when available.
    /// </summary>
    public string? ParentPath { get; init; }
}
