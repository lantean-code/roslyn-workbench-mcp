namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

internal interface IWorkspaceContractValidator<in TSelector>
    where TSelector : class
{
    WorkspaceContractValidationResult Validate(TSelector selector);
}
