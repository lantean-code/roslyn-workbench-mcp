namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-solution-structure.
/// </summary>
[PublishedCollectionResponse(nameof(Projects))]
public sealed record SolutionStructureData
{
    /// <summary>
    /// Gets the loaded solution or project path.
    /// </summary>
    public string SolutionPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the solution folders.
    /// </summary>
    public IReadOnlyList<SolutionFolderInfo> Folders { get; init; } = [];

    /// <summary>
    /// Gets the project structure projections.
    /// </summary>
    public IReadOnlyList<ProjectStructureInfo> Projects { get; init; } = [];

    /// <summary>
    /// Gets the number of projects returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more projects were available.
    /// </summary>
    public bool HasMore { get; init; }
}
