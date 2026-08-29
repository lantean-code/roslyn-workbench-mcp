namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-project-details.
/// </summary>
internal sealed record ProjectDetailsData : IQueryResponse
{
    /// <summary>
    /// Gets the selected project information.
    /// </summary>
    [Description("The selected project information.")]
    public ProjectInfo? Project { get; init; }

    /// <summary>
    /// Gets the project documents, when included.
    /// </summary>
    [Description("The project documents, when included.")]
    public BoundedCollection<DocumentReference>? Documents { get; init; }

    /// <summary>
    /// Gets the direct project references.
    /// </summary>
    [Description("The direct project references.")]
    public BoundedCollection<ProjectReferenceInfo> ProjectReferences { get; init; } = BoundedCollection.Empty<ProjectReferenceInfo>();

    /// <summary>
    /// Gets the metadata references.
    /// </summary>
    [Description("The metadata references.")]
    public BoundedCollection<MetadataReferenceInfo> MetadataReferences { get; init; } = BoundedCollection.Empty<MetadataReferenceInfo>();

    /// <summary>
    /// Gets the analyzer references.
    /// </summary>
    [Description("The analyzer references.")]
    public BoundedCollection<AnalyzerInfo> Analyzers { get; init; } = BoundedCollection.Empty<AnalyzerInfo>();

    /// <summary>
    /// Gets the compilation options.
    /// </summary>
    [Description("The compilation options.")]
    public CompilationOptionsInfo? CompilationOptions { get; init; }
}
