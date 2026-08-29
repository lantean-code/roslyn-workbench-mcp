namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve document options.
/// </summary>
internal sealed record GetDocumentOptionsRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the document selector.
    /// </summary>
    [Description("The document selector.")]
    public required DocumentSelector Document { get; init; }

    /// <summary>
    /// Gets a value indicating whether to include parse options.
    /// </summary>
    [Description("Whether to include parse options.")]
    public bool IncludeParseOptions { get; init; }

    /// <summary>
    /// Gets a value indicating whether to include effective analyser configuration.
    /// </summary>
    [Description("Whether to include effective analyser configuration.")]
    public bool IncludeAnalyzerConfig { get; init; }
}
