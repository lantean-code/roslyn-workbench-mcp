namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-project-details.
/// </summary>
internal sealed record ProjectDetailsData : IQueryResponse
{
    /// <summary>
    /// Gets the selected project information.
    /// </summary>
    public ProjectInfo? Project { get; init; }

    /// <summary>
    /// Gets the project documents, when included.
    /// </summary>
    public BoundedCollection<DocumentReference>? Documents { get; init; }

    /// <summary>
    /// Gets the direct project references.
    /// </summary>
    public BoundedCollection<ProjectReferenceInfo> ProjectReferences { get; init; } = BoundedCollection.Empty<ProjectReferenceInfo>();

    /// <summary>
    /// Gets the metadata references.
    /// </summary>
    public BoundedCollection<MetadataReferenceInfo> MetadataReferences { get; init; } = BoundedCollection.Empty<MetadataReferenceInfo>();

    /// <summary>
    /// Gets the analyzer references.
    /// </summary>
    public BoundedCollection<AnalyzerInfo> Analyzers { get; init; } = BoundedCollection.Empty<AnalyzerInfo>();

    /// <summary>
    /// Gets the compilation options.
    /// </summary>
    public CompilationOptionsInfo? CompilationOptions { get; init; }
}
