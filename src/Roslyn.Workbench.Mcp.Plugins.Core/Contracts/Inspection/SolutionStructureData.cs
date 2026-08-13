namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-solution-structure.
/// </summary>
internal sealed record SolutionStructureData : IQueryResponse
{
    /// <summary>
    /// Gets the loaded solution or project path.
    /// </summary>
    public string SolutionPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the solution folders.
    /// </summary>
    public BoundedCollection<SolutionFolderInfo> Folders { get; init; } = BoundedCollection.Empty<SolutionFolderInfo>();

    /// <summary>
    /// Gets the project structure projections.
    /// </summary>
    public BoundedCollection<ProjectStructureInfo> Projects { get; init; } = BoundedCollection.Empty<ProjectStructureInfo>();
}
