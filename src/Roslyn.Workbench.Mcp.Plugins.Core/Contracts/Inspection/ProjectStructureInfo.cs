namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one project within solution-structure results.
/// </summary>
internal sealed record ProjectStructureInfo
{
    /// <summary>
    /// Gets the project identifier.
    /// </summary>
    [Description("The project identifier.")]
    public required string ProjectId { get; init; }

    /// <summary>
    /// Gets the project name.
    /// </summary>
    [Description("The project name.")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the project path.
    /// </summary>
    [Description("The project path.")]
    public required string Path { get; init; }

    /// <summary>
    /// Gets the associated solution-folder path, when available.
    /// </summary>
    [Description("The associated solution-folder path, when available.")]
    public string? SolutionFolderPath { get; init; }

    /// <summary>
    /// Gets the target frameworks inferred for the project.
    /// </summary>
    [Description("The target frameworks inferred for the project.")]
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];

    /// <summary>
    /// Gets the direct project references.
    /// </summary>
    [Description("The direct project references.")]
    public BoundedCollection<ProjectReferenceInfo> ProjectReferences { get; init; } = BoundedCollection.Empty<ProjectReferenceInfo>();

    /// <summary>
    /// Gets the project documents, when included.
    /// </summary>
    [Description("The project documents, when included.")]
    public BoundedCollection<DocumentReference>? Documents { get; init; }
}
