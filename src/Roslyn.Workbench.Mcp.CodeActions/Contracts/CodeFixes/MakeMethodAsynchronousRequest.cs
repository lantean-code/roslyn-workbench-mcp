namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.CodeFixes;

/// <summary>
/// Requests one supported Roslyn make-method-asynchronous code fix.
/// </summary>
internal sealed record MakeMethodAsynchronousRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the source location containing the asynchronous-method diagnostic to fix.
    /// </summary>
    public required LocationSelector Location { get; init; }

    /// <summary>
    /// Gets the asynchronous method shape to stage.
    /// </summary>
    public required MakeMethodAsynchronousStrategy Strategy { get; init; }
}
