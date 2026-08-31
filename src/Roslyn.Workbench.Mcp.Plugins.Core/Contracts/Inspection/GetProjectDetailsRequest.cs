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
    [Description("Whether documents should be included.")]
    public bool IncludeDocuments { get; init; }

    /// <summary>
    /// Gets the optional documents limit.
    /// </summary>
    [Description("Maximum number of documents to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDocumentsMaxResults)]
    public int? DocumentsLimit { get; init; } = _defaultDocumentsMaxResults;

    /// <summary>
    /// Gets the optional project references limit.
    /// </summary>
    [Description("Maximum number of project references to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultProjectReferencesMaxResults)]
    public int? ProjectReferencesLimit { get; init; } = _defaultProjectReferencesMaxResults;

    /// <summary>
    /// Gets the optional metadata references limit.
    /// </summary>
    [Description("Maximum number of metadata references to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultMetadataReferencesMaxResults)]
    public int? MetadataReferencesLimit { get; init; } = _defaultMetadataReferencesMaxResults;

    /// <summary>
    /// Gets the optional analyzers limit.
    /// </summary>
    [Description("Maximum number of analyzers to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultAnalyzersMaxResults)]
    public int? AnalyzersLimit { get; init; } = _defaultAnalyzersMaxResults;

    /// <summary>
    /// Gets the effective documents limit.
    /// </summary>
    internal int EffectiveDocumentsLimit => ResultLimit.GetEffectiveValue(DocumentsLimit, _defaultDocumentsMaxResults);

    /// <summary>
    /// Gets the effective project references limit.
    /// </summary>
    internal int EffectiveProjectReferencesLimit => ResultLimit.GetEffectiveValue(ProjectReferencesLimit, _defaultProjectReferencesMaxResults);

    /// <summary>
    /// Gets the effective metadata references limit.
    /// </summary>
    internal int EffectiveMetadataReferencesLimit => ResultLimit.GetEffectiveValue(MetadataReferencesLimit, _defaultMetadataReferencesMaxResults);

    /// <summary>
    /// Gets the effective analyzers limit.
    /// </summary>
    internal int EffectiveAnalyzersLimit => ResultLimit.GetEffectiveValue(AnalyzersLimit, _defaultAnalyzersMaxResults);
}
