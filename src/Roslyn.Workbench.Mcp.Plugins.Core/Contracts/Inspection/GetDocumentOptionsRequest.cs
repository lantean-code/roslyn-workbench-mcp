namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve document options.
/// </summary>
internal sealed record GetDocumentOptionsRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the document selector.
    /// </summary>
    public required DocumentSelector Document { get; init; }

    /// <summary>
    /// Gets a value indicating whether to include parse options.
    /// </summary>
    public bool IncludeParseOptions { get; init; }

    /// <summary>
    /// Gets a value indicating whether to include effective analyser configuration.
    /// </summary>
    public bool IncludeAnalyzerConfig { get; init; }
}
