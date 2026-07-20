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

        var folders = new List<SolutionFolderInfo>();
        var foldersHaveMore = false;
        foreach (var folder in hierarchy.Folders)
        {
            if (folders.Count == request.EffectiveFoldersLimit)
            {
                foldersHaveMore = true;
                break;
            }

            folders.Add(folder);
        }

        var orderedProjects = context.CurrentSolution.Projects
            .OrderBy(project => context.WorkspaceResolver.NormalizeProjectPath(project.FilePath ?? project.Name), StringComparer.Ordinal);

        var selectedProjects = new List<Project>();
        var projectsHaveMore = false;
        foreach (var project in orderedProjects)
        {
            if (selectedProjects.Count == request.EffectiveProjectsLimit)
            {
                projectsHaveMore = true;
                break;
            }

            selectedProjects.Add(project);
        }

        var projectStructures = new List<ProjectStructureInfo>();
        foreach (var project in selectedProjects)
        {
            var targetFrameworks = context.ToolExecutionServices.ProjectStructureService.GetTargetFrameworks(project);
            if (!targetFrameworks.IsSucceeded)
            {
                return ToolExecutionHelpers.RejectProjectStructureFailure<SolutionStructureData>(targetFrameworks.ErrorMessage);
            }

            var projectPath = context.WorkspaceResolver.NormalizeProjectPath(project.FilePath ?? project.Name);
            var solutionFolderPath = hierarchy.ProjectFolderPaths.TryGetValue(projectPath, out var folderPath)
                ? folderPath
                : null;

            var projectedProjectReferences = new List<ProjectReferenceInfo>();
            foreach (var reference in project.ProjectReferences)
            {
                var referencedProject = context.CurrentSolution.GetProject(reference.ProjectId);
                if (referencedProject is not null)
                {
                    projectedProjectReferences.Add(InspectionProjectionFactory.CreateProjectReferenceInfo(referencedProject, context.WorkspaceResolver));
                }
            }

            var projectReferences = projectedProjectReferences
                .OrderBy(static reference => reference.Path, StringComparer.Ordinal)
                .ToArray();

            IReadOnlyList<DocumentReference>? documents = null;
            if (request.IncludeDocuments)
            {
                var orderedDocuments = project.Documents
                    .OrderBy(document => context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name), StringComparer.Ordinal)
                    .ToArray();

                var projectedDocuments = new List<DocumentReference>();
                foreach (var document in orderedDocuments)
                {
                    var documentReference = context.WorkspaceResolver.CreateDocumentReference(document);
                    if (documentReference is not null)
                    {
                        projectedDocuments.Add(documentReference);
                    }
                }

                documents = projectedDocuments;
            }

            projectStructures.Add(new ProjectStructureInfo
            {
                ProjectId = project.Id.Id.ToString(),
                Name = project.Name,
                Path = projectPath,
                SolutionFolderPath = solutionFolderPath,
                TargetFrameworks = targetFrameworks.TargetFrameworks,
                ProjectReferences = projectReferences,
                Documents = documents,
            });
        }

        var data = new SolutionStructureData
        {
            SolutionPath = context.WorkspaceIdentity.LoadedPath,
            Folders = ToolExecutionHelpers.CreatePreboundedCollection(folders, foldersHaveMore),
            Projects = ToolExecutionHelpers.CreatePreboundedCollection(projectStructures, projectsHaveMore),
        };

        return PluginExecutionResult<SolutionStructureData>.Success(data);
    }
}
