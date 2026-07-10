namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Represents one solution folder and its hierarchy position.
/// </summary>
public sealed record SolutionFolderInfo
{
    /// <summary>
    /// Gets the folder display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the canonical folder path within the solution hierarchy.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parent folder path, when available.
    /// </summary>
    public string? ParentPath { get; init; }
}
