namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

/// <summary>
/// Resolves workspace scopes into the documents and projects eligible for Code Action execution.
/// </summary>
internal interface ICodeActionScopeResolver
{
    /// <summary>
    /// Resolves the Code Action scope.
    /// </summary>
    /// <param name="scope">The workspace scope to which the operation applies.</param>
    /// <param name="solution">The solution to inspect or modify.</param>
    /// <param name="workspaceResolver">The resolver used to resolve selectors and enumerate addressable documents.</param>
    /// <returns>The selected documents and projects, or a rejection when the scope is invalid.</returns>
    CodeActionScopeResolution Resolve(
        ScopeSelector scope,
        Solution solution,
        IWorkspaceResolver workspaceResolver);
}
