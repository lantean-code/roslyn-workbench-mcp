namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-solution-structure", "Get Solution Structure", "Returns solution folders, projects, target frameworks and direct project relationships.")]
internal sealed class GetSolutionStructureTool : QueryToolHandler<GetSolutionStructureRequest, SolutionStructureData>
{
    protected override async ValueTask<PluginExecutionResult<SolutionStructureData>> ExecuteCoreAsync(GetSolutionStructureRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var hierarchy = await context.ToolExecutionServices.ProjectStructureService.GetSolutionHierarchyAsync(context.WorkspaceIdentity.LoadedPath, cancellationToken);
        if (!hierarchy.IsSucceeded)
        {
            return ToolExecutionHelpers.RejectProjectStructureFailure<SolutionStructureData>(hierarchy.ErrorMessage);
        }

        var projects = context.CurrentSolution.Projects
            .OrderBy(project => context.WorkspaceResolver.NormalizeProjectPath(project.FilePath ?? project.Name), StringComparer.Ordinal)
            .ToArray();
        var targetFrameworksByProject = new Dictionary<ProjectId, IReadOnlyList<string>>();
        foreach (var project in projects)
        {
            var targetFrameworks = context.ToolExecutionServices.ProjectStructureService.GetTargetFrameworks(project);
            if (!targetFrameworks.IsSucceeded)
            {
                return ToolExecutionHelpers.RejectProjectStructureFailure<SolutionStructureData>(targetFrameworks.ErrorMessage);
            }

            targetFrameworksByProject[project.Id] = targetFrameworks.TargetFrameworks;
        }

        var projectStructures = projects
            .Select(project => new ProjectStructureInfo
            {
                ProjectId = project.Id.Id.ToString(),
                Name = project.Name,
                Path = context.WorkspaceResolver.NormalizeProjectPath(project.FilePath ?? project.Name),
                SolutionFolderPath = hierarchy.ProjectFolderPaths.TryGetValue(context.WorkspaceResolver.NormalizeProjectPath(project.FilePath ?? project.Name), out var solutionFolderPath)
                    ? solutionFolderPath
                    : null,
                TargetFrameworks = targetFrameworksByProject[project.Id],
                ProjectReferences = project.ProjectReferences
                    .Select(reference => context.CurrentSolution.GetProject(reference.ProjectId))
                    .OfType<Project>()
                    .Select(project => InspectionProjectionFactory.CreateProjectReferenceInfo(project, context.WorkspaceResolver))
                    .OrderBy(static reference => reference.Path, StringComparer.Ordinal)
                    .ToArray(),
                Documents = request.IncludeDocuments
                    ? project.Documents
                        .OrderBy(document => context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name), StringComparer.Ordinal)
                        .Select(document => context.WorkspaceResolver.CreateDocumentReference(document))
                        .OfType<DocumentReference>()
                        .ToArray()
                    : null,
            })
            .ToArray();

        return PluginExecutionResult<SolutionStructureData>.Success(new SolutionStructureData
        {
            SolutionPath = context.WorkspaceIdentity.LoadedPath,
            Folders = ToolExecutionHelpers.CreateBoundedCollection(
                hierarchy.Folders,
                ToolExecutionHelpers.GetMaxResults(request.FoldersLimit, GetSolutionStructureRequest._defaultFoldersMaxResults)),
            Projects = ToolExecutionHelpers.CreateBoundedCollection(
                projectStructures,
                ToolExecutionHelpers.GetMaxResults(request.ProjectsLimit, GetSolutionStructureRequest._defaultProjectsMaxResults)),
        });
    }
}
