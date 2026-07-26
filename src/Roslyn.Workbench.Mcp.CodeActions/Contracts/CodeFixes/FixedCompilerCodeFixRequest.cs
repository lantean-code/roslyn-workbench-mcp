namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.CodeFixes;

/// <summary>
/// Requests a source location for a tool that fixes one predetermined compiler diagnostic.
/// </summary>
internal sealed record FixedCompilerCodeFixRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the source location containing the compiler diagnostic to fix.
    /// </summary>
    public required LocationSelector Location { get; init; }
}
