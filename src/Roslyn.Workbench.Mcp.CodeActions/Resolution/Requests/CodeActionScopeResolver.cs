namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

/// <summary>
/// Resolves a workspace scope into the documents and projects eligible for Code Action execution.
/// </summary>
internal sealed class CodeActionScopeResolver : ICodeActionScopeResolver
{
    /// <summary>
    /// Resolves the Code Action scope.
    /// </summary>
    /// <param name="scope">The workspace scope to which the operation applies.</param>
    /// <param name="solution">The solution to inspect or modify.</param>
    /// <param name="workspaceResolver">The resolver used to resolve selectors and enumerate addressable documents.</param>
    /// <returns>The selected documents and projects, or a rejection when the scope is invalid.</returns>
    public CodeActionScopeResolution Resolve(
        ScopeSelector scope,
        Solution solution,
        IWorkspaceResolver workspaceResolver)
    {
        return scope.Kind switch
        {
            ScopeKind.Solution => CodeActionScopeResolution.Resolved(workspaceResolver.GetDocuments(solution)),
            ScopeKind.Document => ResolveDocument(scope.Document, workspaceResolver),
            ScopeKind.Project => ResolveProject(scope.Project, workspaceResolver),
            ScopeKind.Projects => ResolveProjects(scope.Projects, workspaceResolver),
            _ => InvalidRequest("The requested scope kind is not supported."),
        };
    }

    private static CodeActionScopeResolution ResolveDocument(
        DocumentSelector? selector,
        IWorkspaceResolver workspaceResolver)
    {
        if (selector is null)
        {
            return InvalidRequest("Document scope requires a document selector.");
        }

        var resolution = workspaceResolver.ResolveDocument(selector);
        if (resolution.Status != SelectorResolveStatus.Resolved || resolution.Value is null)
        {
            return SelectorFailure(resolution.Status, "Document", "document");
        }

        return CodeActionScopeResolution.Resolved([resolution.Value]);
    }

    private static CodeActionScopeResolution ResolveProject(
        ProjectSelector? selector,
        IWorkspaceResolver workspaceResolver)
    {
        if (selector is null)
        {
            return InvalidRequest("Project scope requires a project selector.");
        }

        var resolution = workspaceResolver.ResolveProject(selector);
        if (resolution.Status != SelectorResolveStatus.Resolved || resolution.Value is null)
        {
            return SelectorFailure(resolution.Status, "Project", "project");
        }

        return FromProject(resolution.Value, workspaceResolver);
    }

    private static CodeActionScopeResolution ResolveProjects(
        IReadOnlyList<ProjectSelector>? selectors,
        IWorkspaceResolver workspaceResolver)
    {
        if (selectors is null || selectors.Count == 0)
        {
            return InvalidRequest("Projects scope requires at least one project selector.");
        }

        var projects = new List<Project>();
        foreach (var selector in selectors)
        {
            var resolution = workspaceResolver.ResolveProject(selector);
            if (resolution.Status != SelectorResolveStatus.Resolved || resolution.Value is null)
            {
                return SelectorFailure(resolution.Status, "Project", "project");
            }

            projects.Add(resolution.Value);
        }

        var distinctProjects = projects.DistinctBy(static project => project.Id).ToArray();
        var documents = new List<Document>();
        foreach (var project in distinctProjects)
        {
            documents.AddRange(workspaceResolver.GetDocuments(project));
        }

        return CodeActionScopeResolution.Resolved(documents, distinctProjects);
    }

    private static CodeActionScopeResolution FromProject(Project project, IWorkspaceResolver workspaceResolver)
    {
        return CodeActionScopeResolution.Resolved(workspaceResolver.GetDocuments(project), [project]);
    }

    private static CodeActionScopeResolution InvalidRequest(string message)
    {
        var rejection = CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>(
            "InvalidRequest",
            message);

        return CodeActionScopeResolution.Rejected(rejection);
    }

    private static CodeActionScopeResolution SelectorFailure(
        SelectorResolveStatus status,
        string targetCode,
        string targetDisplayName)
    {
        var rejection = CodeActionExecutionResultFactory.RejectFromStatus<WorkspaceMutationCandidate>(
            status,
            targetCode,
            targetDisplayName);

        return CodeActionScopeResolution.Rejected(rejection);
    }
}
