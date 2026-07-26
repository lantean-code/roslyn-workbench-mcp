namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.CodeFixes;

/// <summary>
/// Requests addition of missing using directives across a selected scope.
/// </summary>
internal sealed record AddMissingUsingsRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the scope to clean.
    /// </summary>
    public required ScopeSelector Scope { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request prefers global using directives.
    /// </summary>
    public bool PreferGlobalUsings { get; init; }
}
