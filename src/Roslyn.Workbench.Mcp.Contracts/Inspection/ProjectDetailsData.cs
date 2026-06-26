using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-project-details.
/// </summary>
public sealed record ProjectDetailsData
{
    /// <summary>
    /// Gets the selected project information.
    /// </summary>
    public ProjectInfo? Project { get; init; }

    /// <summary>
    /// Gets the project documents, when included.
    /// </summary>
    public IReadOnlyList<DocumentReference>? Documents { get; init; }

    /// <summary>
    /// Gets the direct project references.
    /// </summary>
    public IReadOnlyList<ProjectReferenceInfo> ProjectReferences { get; init; } = [];

    /// <summary>
    /// Gets the metadata references.
    /// </summary>
    public IReadOnlyList<MetadataReferenceInfo> MetadataReferences { get; init; } = [];

    /// <summary>
    /// Gets the analyzer references.
    /// </summary>
    public IReadOnlyList<AnalyzerInfo> Analyzers { get; init; } = [];

    /// <summary>
    /// Gets the compilation options.
    /// </summary>
    public CompilationOptionsInfo? CompilationOptions { get; init; }

    /// <summary>
    /// Gets the number of documents returned, when document enumeration is enabled.
    /// </summary>
    public int? ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more documents were available.
    /// </summary>
    public bool? HasMore { get; init; }
}
