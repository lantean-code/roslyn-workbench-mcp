namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

internal sealed class ProjectSelectorValidator : IWorkspaceContractValidator<ProjectSelector>
{
    public WorkspaceContractValidationResult Validate(ProjectSelector selector)
    {
        if (!string.IsNullOrWhiteSpace(selector.ProjectId)
            || !string.IsNullOrWhiteSpace(selector.Name)
            || !string.IsNullOrWhiteSpace(selector.Path)
            || !string.IsNullOrWhiteSpace(selector.TargetFramework))
        {
            return WorkspaceContractValidationResult.Valid();
        }

        var memberNames = new[]
        {
            nameof(ProjectSelector.ProjectId),
            nameof(ProjectSelector.Name),
            nameof(ProjectSelector.Path),
            nameof(ProjectSelector.TargetFramework),
        };

        var failures = new[]
        {
            new WorkspaceContractValidationFailure(
                "ProjectSelector must provide at least one of ProjectId, Name, Path, or TargetFramework.",
                memberNames),
        };

        return WorkspaceContractValidationResult.Invalid(failures);
    }
}
