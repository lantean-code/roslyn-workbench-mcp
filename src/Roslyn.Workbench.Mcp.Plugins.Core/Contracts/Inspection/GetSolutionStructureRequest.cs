namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve solution structure.
/// </summary>
internal sealed record GetSolutionStructureRequest : WorkspaceBoundRequest
{
    private const int _defaultFoldersMaxResults = 200;
    private const int _defaultDocumentsPerProjectMaxResults = 200;
    private const int _defaultProjectsMaxResults = 100;
    private const int _defaultProjectReferencesPerProjectMaxResults = 50;

    /// <summary>
    /// Gets a value indicating whether documents should be included in project projections.
    /// </summary>
    public bool IncludeDocuments { get; init; }

    /// <summary>
    /// Gets the optional folders limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultFoldersMaxResults)]
    public int? FoldersLimit { get; init; } = _defaultFoldersMaxResults;

    /// <summary>
    /// Gets the optional projects limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultProjectsMaxResults)]
    public int? ProjectsLimit { get; init; } = _defaultProjectsMaxResults;

    /// <summary>
    /// Gets the optional document limit applied independently to each returned project.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDocumentsPerProjectMaxResults)]
    public int? DocumentsPerProjectLimit { get; init; } = _defaultDocumentsPerProjectMaxResults;

    /// <summary>
    /// Gets the optional direct project-reference limit applied independently to each returned project.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultProjectReferencesPerProjectMaxResults)]
    public int? ProjectReferencesPerProjectLimit { get; init; } = _defaultProjectReferencesPerProjectMaxResults;

    internal int EffectiveFoldersLimit => ResultLimit.GetEffectiveValue(FoldersLimit, _defaultFoldersMaxResults);

    internal int EffectiveProjectsLimit => ResultLimit.GetEffectiveValue(ProjectsLimit, _defaultProjectsMaxResults);

    internal int EffectiveDocumentsPerProjectLimit => ResultLimit.GetEffectiveValue(DocumentsPerProjectLimit, _defaultDocumentsPerProjectMaxResults);

    internal int EffectiveProjectReferencesPerProjectLimit => ResultLimit.GetEffectiveValue(ProjectReferencesPerProjectLimit, _defaultProjectReferencesPerProjectMaxResults);
}
