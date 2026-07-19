namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve solution structure.
/// </summary>
public sealed record GetSolutionStructureRequest : WorkspaceBoundRequest
{
    private const int _defaultFoldersMaxResults = 200;
    private const int _defaultProjectsMaxResults = 100;

    /// <summary>
    /// Gets a value indicating whether documents should be included in project projections.
    /// </summary>
    public bool IncludeDocuments { get; init; }

    /// <summary>
    /// Gets the optional folders limit.
    /// </summary>
    [DefaultValue(_defaultFoldersMaxResults)]
    public int? FoldersLimit { get; init; } = _defaultFoldersMaxResults;

    /// <summary>
    /// Gets the optional projects limit.
    /// </summary>
    [DefaultValue(_defaultProjectsMaxResults)]
    public int? ProjectsLimit { get; init; } = _defaultProjectsMaxResults;

    internal int EffectiveFoldersLimit => ToolExecutionHelpers.GetMaxResults(FoldersLimit, _defaultFoldersMaxResults);

    internal int EffectiveProjectsLimit => ToolExecutionHelpers.GetMaxResults(ProjectsLimit, _defaultProjectsMaxResults);
}
