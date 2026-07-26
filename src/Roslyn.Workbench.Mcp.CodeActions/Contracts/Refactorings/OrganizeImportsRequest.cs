namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests Roslyn import organisation for one document.
/// </summary>
internal sealed record OrganizeImportsRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the document whose imports should be organised.
    /// </summary>
    public required DocumentSelector Document { get; init; }
}
