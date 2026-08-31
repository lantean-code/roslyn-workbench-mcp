namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Identifies a request that consumes a previously issued Code Action reference.
/// </summary>
internal interface ICodeActionReferenceRequest
{
    /// <summary>
    /// Gets the opaque identifier of the referenced Code Action.
    /// </summary>
    Guid ActionId { get; }
}
