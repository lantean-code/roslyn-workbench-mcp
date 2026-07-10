using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to format one document or a selected range.
/// </summary>
public sealed record FormatDocumentRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the document selector.
    /// </summary>
    public DocumentSelector? Document { get; init; }

    /// <summary>
    /// Gets the optional range to format.
    /// </summary>
    public TextSpanSelector? Range { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected document.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
