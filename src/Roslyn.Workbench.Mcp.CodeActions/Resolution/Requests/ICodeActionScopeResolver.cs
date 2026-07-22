namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

internal interface ICodeActionScopeResolver
{
    CodeActionScopeResolution Resolve(
        ScopeSelector scope,
        Solution solution,
        IWorkspaceResolver workspaceResolver);
}
