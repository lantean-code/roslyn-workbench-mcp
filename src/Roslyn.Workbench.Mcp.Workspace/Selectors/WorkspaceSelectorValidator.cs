namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

internal sealed class WorkspaceSelectorValidator : IWorkspaceContractValidator<WorkspaceSelector>
{
    public WorkspaceContractValidationResult Validate(WorkspaceSelector selector)
    {
        if (selector.WorkspaceId is not null
            || !string.IsNullOrWhiteSpace(selector.Alias)
            || !string.IsNullOrWhiteSpace(selector.Path))
        {
            return WorkspaceContractValidationResult.Valid();
        }

        var memberNames = new[]
        {
            nameof(WorkspaceSelector.WorkspaceId),
            nameof(WorkspaceSelector.Alias),
            nameof(WorkspaceSelector.Path),
        };

        var failures = new[]
        {
            new WorkspaceContractValidationFailure(
                "WorkspaceSelector must provide at least one of WorkspaceId, Alias, or Path.",
                memberNames),
        };

        return WorkspaceContractValidationResult.Invalid(failures);
    }
}
