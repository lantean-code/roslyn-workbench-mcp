namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to sort using directives within one document.
/// </summary>
internal sealed record SortUsingsRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the document selector.
    /// </summary>
    public required DocumentSelector Document { get; init; }

    /// <summary>
    /// Gets a value indicating whether system namespaces should sort first.
    /// </summary>
    public bool SystemFirst { get; init; }
}
