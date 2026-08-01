namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

internal sealed class ScopeSelectorValidator : IWorkspaceContractValidator<ScopeSelector>
{
    public WorkspaceContractValidationResult Validate(ScopeSelector selector)
    {
        var failures = new List<WorkspaceContractValidationFailure>();

        switch (selector.Kind)
        {
            case ScopeKind.Solution:
                ValidateSolution(selector, failures);
                break;

            case ScopeKind.Project:
                ValidateProject(selector, failures);
                break;

            case ScopeKind.Document:
                ValidateDocument(selector, failures);
                break;

            case ScopeKind.Projects:
                ValidateProjects(selector, failures);
                break;
        }

        if (failures.Count == 0)
        {
            return WorkspaceContractValidationResult.Valid();
        }

        return WorkspaceContractValidationResult.Invalid(failures);
    }

    private static void ValidateSolution(
        ScopeSelector selector,
        List<WorkspaceContractValidationFailure> failures)
    {
        if (selector.Project is null && selector.Document is null && selector.Projects is null)
        {
            return;
        }

        failures.Add(CreateFailure(
            "ScopeSelector Kind Solution must not provide Project, Document, or Projects.",
            nameof(ScopeSelector.Project),
            nameof(ScopeSelector.Document),
            nameof(ScopeSelector.Projects)));
    }

    private static void ValidateProject(
        ScopeSelector selector,
        List<WorkspaceContractValidationFailure> failures)
    {
        if (selector.Project is null)
        {
            failures.Add(CreateFailure(
                "ScopeSelector Kind Project must provide Project.",
                nameof(ScopeSelector.Project)));
        }

        if (selector.Document is not null || selector.Projects is not null)
        {
            failures.Add(CreateFailure(
                "ScopeSelector Kind Project must not provide Document or Projects.",
                nameof(ScopeSelector.Document),
                nameof(ScopeSelector.Projects)));
        }
    }

    private static void ValidateDocument(
        ScopeSelector selector,
        List<WorkspaceContractValidationFailure> failures)
    {
        if (selector.Document is null)
        {
            failures.Add(CreateFailure(
                "ScopeSelector Kind Document must provide Document.",
                nameof(ScopeSelector.Document)));
        }

        if (selector.Project is not null || selector.Projects is not null)
        {
            failures.Add(CreateFailure(
                "ScopeSelector Kind Document must not provide Project or Projects.",
                nameof(ScopeSelector.Project),
                nameof(ScopeSelector.Projects)));
        }
    }

    private static void ValidateProjects(
        ScopeSelector selector,
        List<WorkspaceContractValidationFailure> failures)
    {
        if (selector.Projects is null || selector.Projects.Count == 0)
        {
            failures.Add(CreateFailure(
                "ScopeSelector Kind Projects must provide at least one Project selector.",
                nameof(ScopeSelector.Projects)));
        }

        if (selector.Project is not null || selector.Document is not null)
        {
            failures.Add(CreateFailure(
                "ScopeSelector Kind Projects must not provide Project or Document.",
                nameof(ScopeSelector.Project),
                nameof(ScopeSelector.Document)));
        }
    }

    private static WorkspaceContractValidationFailure CreateFailure(
        string message,
        params string[] memberNames)
    {
        return new WorkspaceContractValidationFailure(message, memberNames);
    }
}
