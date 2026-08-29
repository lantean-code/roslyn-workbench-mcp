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
    [Description("The effective language version.")]
    public string LanguageVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the effective nullable context.
    /// </summary>
    [Description("The effective nullable context.")]
    public string NullableContext { get; init; } = string.Empty;

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
