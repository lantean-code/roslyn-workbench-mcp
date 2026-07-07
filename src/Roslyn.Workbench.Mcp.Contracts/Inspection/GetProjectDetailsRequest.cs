using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve project details.
/// </summary>
public sealed record GetProjectDetailsRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the project selector.
    /// </summary>
    public ProjectSelector? Project { get; init; }

    /// <summary>
    /// Gets a value indicating whether documents should be included.
    /// </summary>
    public bool IncludeDocuments { get; init; }

    /// <summary>
    /// Gets the optional documents limit.
    /// </summary>
    public CollectionLimit? DocumentsLimit { get; init; }

    /// <summary>
    /// Gets the optional project references limit.
    /// </summary>
    public CollectionLimit? ProjectReferencesLimit { get; init; }

    /// <summary>
    /// Gets the optional metadata references limit.
    /// </summary>
    public CollectionLimit? MetadataReferencesLimit { get; init; }

    /// <summary>
    /// Gets the optional analyzers limit.
    /// </summary>
    public CollectionLimit? AnalyzersLimit { get; init; }
}
