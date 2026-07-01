using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to sort using directives within one document.
/// </summary>
public sealed record SortUsingsRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the document selector.
    /// </summary>
    public DocumentSelector? Document { get; init; }

    /// <summary>
    /// Gets a value indicating whether system namespaces should sort first.
    /// </summary>
    public bool SystemFirst { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected document.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
