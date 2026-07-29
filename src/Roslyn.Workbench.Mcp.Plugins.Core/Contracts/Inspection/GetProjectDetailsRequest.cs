namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve project details.
/// </summary>
internal sealed record GetProjectDetailsRequest : WorkspaceBoundRequest
{
    private const int _defaultAnalyzersMaxResults = 50;
    private const int _defaultDocumentsMaxResults = 200;
    private const int _defaultMetadataReferencesMaxResults = 100;
    private const int _defaultProjectReferencesMaxResults = 50;

    /// <summary>
    /// Gets the project selector.
    /// </summary>
    public required ProjectSelector Project { get; init; }

    /// <summary>
    /// Gets a value indicating whether documents should be included.
    /// </summary>
    public bool IncludeDocuments { get; init; }

    /// <summary>
    /// Gets the optional documents limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDocumentsMaxResults)]
    public int? DocumentsLimit { get; init; } = _defaultDocumentsMaxResults;

    /// <summary>
    /// Gets the optional project references limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultProjectReferencesMaxResults)]
    public int? ProjectReferencesLimit { get; init; } = _defaultProjectReferencesMaxResults;

    /// <summary>
    /// Gets the optional metadata references limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultMetadataReferencesMaxResults)]
    public int? MetadataReferencesLimit { get; init; } = _defaultMetadataReferencesMaxResults;

    /// <summary>
    /// Gets the optional analyzers limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultAnalyzersMaxResults)]
    public int? AnalyzersLimit { get; init; } = _defaultAnalyzersMaxResults;

    internal int EffectiveDocumentsLimit => ResultLimit.GetEffectiveValue(DocumentsLimit, _defaultDocumentsMaxResults);

    internal int EffectiveProjectReferencesLimit => ResultLimit.GetEffectiveValue(ProjectReferencesLimit, _defaultProjectReferencesMaxResults);

    internal int EffectiveMetadataReferencesLimit => ResultLimit.GetEffectiveValue(MetadataReferencesLimit, _defaultMetadataReferencesMaxResults);

    internal int EffectiveAnalyzersLimit => ResultLimit.GetEffectiveValue(AnalyzersLimit, _defaultAnalyzersMaxResults);
}
