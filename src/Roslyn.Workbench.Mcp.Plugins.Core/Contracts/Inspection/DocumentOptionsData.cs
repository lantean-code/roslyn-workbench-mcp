namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-document-options.
/// </summary>
internal sealed record DocumentOptionsData
{
    /// <summary>
    /// Gets the resolved document reference.
    /// </summary>
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Gets the effective language version.
    /// </summary>
    public string LanguageVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the effective nullable context.
    /// </summary>
    public string NullableContext { get; init; } = string.Empty;

    /// <summary>
    /// Gets the effective parse options.
    /// </summary>
    public ParseOptionsInfo? ParseOptions { get; init; }

    /// <summary>
    /// Gets the effective analyzer-config information.
    /// </summary>
    public AnalyzerConfigInfo? AnalyzerConfig { get; init; }
}
