namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-document-options.
/// </summary>
internal sealed record DocumentOptionsData : IQueryResponse
{
    /// <summary>
    /// Gets the resolved document reference.
    /// </summary>
    [Description("The resolved document reference.")]
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Gets the effective language version.
    /// </summary>
    [Description("The effective language version, when available for the document language.")]
    public string? LanguageVersion { get; init; }

    /// <summary>
    /// Gets the effective nullable context.
    /// </summary>
    [Description("The effective nullable context, when available for the document language.")]
    public string? NullableContext { get; init; }

    /// <summary>
    /// Gets the effective parse options.
    /// </summary>
    [Description("The effective parse options.")]
    public ParseOptionsInfo? ParseOptions { get; init; }

    /// <summary>
    /// Gets the effective analyzer-config information.
    /// </summary>
    [Description("The effective analyzer-config information.")]
    public AnalyzerConfigInfo? AnalyzerConfig { get; init; }
}
