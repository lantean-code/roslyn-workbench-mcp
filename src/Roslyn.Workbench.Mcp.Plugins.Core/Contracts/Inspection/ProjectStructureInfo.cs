namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one project within solution-structure results.
/// </summary>
public sealed record ProjectStructureInfo
{
    /// <summary>
    /// Gets the project identifier.
    /// </summary>
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the project name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the project path.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the associated solution-folder path, when available.
    /// </summary>
    public string? SolutionFolderPath { get; init; }

    /// <summary>
    /// Gets the target frameworks inferred for the project.
    /// </summary>
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];

    /// <summary>
    /// Gets the direct project references.
    /// </summary>
    public IReadOnlyList<ProjectReferenceInfo> ProjectReferences { get; init; } = [];

    /// <summary>
    /// Gets the project documents, when included.
    /// </summary>
    public IReadOnlyList<DocumentReference>? Documents { get; init; }
}
