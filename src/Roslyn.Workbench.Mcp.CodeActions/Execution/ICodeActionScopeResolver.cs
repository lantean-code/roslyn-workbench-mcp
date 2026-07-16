namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionScopeResolver
{
    CodeActionScopeResolution Resolve(
        ScopeSelector scope,
        Solution solution,
        IWorkspaceResolver workspaceResolver);
}
