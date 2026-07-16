using static Roslyn.Workbench.Mcp.CodeActions.Execution.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class CodeActionScopeResolver : ICodeActionScopeResolver
{
    public CodeActionScopeResolution Resolve(
        ScopeSelector scope,
        Solution solution,
        IWorkspaceResolver workspaceResolver)
    {
        return scope.Kind switch
        {
            ScopeKind.Solution => new CodeActionScopeResolution
            {
                Documents = solution.Projects.SelectMany(static project => project.Documents).ToArray(),
            },
            ScopeKind.Document => ResolveDocument(scope.Document, workspaceResolver),
            ScopeKind.Project => ResolveProject(scope.Project, workspaceResolver),
            ScopeKind.Projects => ResolveProjects(scope.Projects, workspaceResolver),
            _ => Reject("The requested scope kind is not supported."),
        };
    }

    private static CodeActionScopeResolution ResolveDocument(
        DocumentSelector? selector,
        IWorkspaceResolver workspaceResolver)
    {
        if (selector is null)
        {
            return Reject("Document scope requires a document selector.");
        }

        var resolution = workspaceResolver.ResolveDocument(selector);
        return resolution.Status == SelectorResolveStatus.Resolved && resolution.Value is not null
            ? new CodeActionScopeResolution
            {
                Documents = [resolution.Value],
            }
            : Reject(RejectFromStatus<WorkspaceMutationCandidate>(resolution.Status, "Document"));
    }

    private static CodeActionScopeResolution ResolveProject(
        ProjectSelector? selector,
        IWorkspaceResolver workspaceResolver)
    {
        if (selector is null)
        {
            return Reject("Project scope requires a project selector.");
        }

        var resolution = workspaceResolver.ResolveProject(selector);
        return resolution.Status == SelectorResolveStatus.Resolved && resolution.Value is not null
            ? FromProject(resolution.Value)
            : Reject(RejectFromStatus<WorkspaceMutationCandidate>(resolution.Status, "Project"));
    }

    private static CodeActionScopeResolution ResolveProjects(
        IReadOnlyList<ProjectSelector>? selectors,
        IWorkspaceResolver workspaceResolver)
    {
        if (selectors is null || selectors.Count == 0)
        {
            return Reject("Projects scope requires at least one project selector.");
        }

        var projects = new List<Project>();
        foreach (var selector in selectors)
        {
            var resolution = workspaceResolver.ResolveProject(selector);
            if (resolution.Status != SelectorResolveStatus.Resolved || resolution.Value is null)
            {
                return Reject(RejectFromStatus<WorkspaceMutationCandidate>(resolution.Status, "Project"));
            }

            projects.Add(resolution.Value);
        }

        var distinctProjects = projects.DistinctBy(static project => project.Id).ToArray();
        return new CodeActionScopeResolution
        {
            Documents = distinctProjects.SelectMany(static project => project.Documents).ToArray(),
            Projects = distinctProjects,
        };
    }

    private static CodeActionScopeResolution FromProject(Project project)
    {
        return new CodeActionScopeResolution
        {
            Documents = project.Documents.ToArray(),
            Projects = [project],
        };
    }

    private static CodeActionScopeResolution Reject(string message)
    {
        return Reject(Rejected<WorkspaceMutationCandidate>("InvalidRequest", message));
    }

    private static CodeActionScopeResolution Reject(
        CodeActionExecutionResult<WorkspaceMutationCandidate> rejection)
    {
        return new CodeActionScopeResolution
        {
            Rejection = rejection,
        };
    }
}
