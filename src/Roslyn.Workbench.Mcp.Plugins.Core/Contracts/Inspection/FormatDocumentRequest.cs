namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to format one document or a selected range.
/// </summary>
internal sealed record FormatDocumentRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the document selector.
    /// </summary>
    public required DocumentSelector Document { get; init; }

    /// <summary>
    /// Gets the optional range to format.
    /// </summary>
    public TextSpanRange? Range { get; init; }
}
