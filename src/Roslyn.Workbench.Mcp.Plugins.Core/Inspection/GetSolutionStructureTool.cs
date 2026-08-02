using Roslyn.Workbench.Mcp.Workspace.Diagnostics;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool(_toolName, "Get Solution Structure", "Returns solution folders, projects, target frameworks and direct project relationships.")]
internal sealed class GetSolutionStructureTool : QueryToolHandler<GetSolutionStructureRequest, SolutionStructureData>
{
    private const string _toolName = "get-solution-structure";

    protected override async ValueTask<PluginExecutionResult<SolutionStructureData>> ExecuteCoreAsync(GetSolutionStructureRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        SolutionHierarchyResult hierarchy;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.SolutionHierarchyPhase))
        {
            hierarchy = await context.ToolExecutionServices.ProjectStructureService.GetSolutionHierarchyAsync(
                context.WorkspaceIdentity,
                cancellationToken);
        }

        if (!hierarchy.IsSucceeded)
        {
            return PluginExecutionResult.Rejected<SolutionStructureData>(
                "ProjectStructureUnavailable",
                hierarchy.ErrorMessage,
                RequiredAction.Retry);
        }

        var folders = new List<SolutionFolderInfo>();
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.FolderSelectionPhase))
        {
            foreach (var folder in hierarchy.Folders)
            {
                if (folders.Count == request.EffectiveFoldersLimit)
                {
                    break;
                }

                folders.Add(folder);
            }
        }

        var selectedProjectEntries = new List<(Project Project, string Path)>();
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.ProjectSelectionPhase))
        {
            var projectEntries = new List<(Project Project, string Path)>();
            foreach (var project in context.CurrentSolution.Projects)
            {
                if (!context.WorkspacePathService.TryNormalizePath(project.FilePath ?? project.Name, out var projectPath))
                {
                    return PluginExecutionResult.Rejected<SolutionStructureData>(
                        "ProjectStructureUnavailable",
                        "A loaded project's path could not be normalized relative to the workspace root.",
                        RequiredAction.ReloadWorkspace);
                }

                projectEntries.Add((project, projectPath));
            }

            projectEntries.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));

            foreach (var projectEntry in projectEntries)
            {
                if (selectedProjectEntries.Count == request.EffectiveProjectsLimit)
                {
                    break;
                }

                selectedProjectEntries.Add(projectEntry);
            }
        }

        var selectedProjects = selectedProjectEntries
            .Select(static entry => entry.Project)
            .ToArray();

        IReadOnlyList<ProjectTargetFrameworksResult> targetFrameworkResults;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.TargetFrameworkEvaluationPhase))
        {
            targetFrameworkResults = context.ToolExecutionServices.ProjectTargetFrameworkResolver.Resolve(
                context.WorkspaceIdentity.WorkspaceId,
                selectedProjects,
                cancellationToken);
        }

        var projectStructures = new List<ProjectStructureInfo>();
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.ProjectProjectionPhase))
        {
            for (var index = 0; index < selectedProjects.Length; index++)
            {
                var projectEntry = selectedProjectEntries[index];
                var project = projectEntry.Project;
                var targetFrameworks = targetFrameworkResults[index];

                if (!targetFrameworks.IsSucceeded)
                {
                    return PluginExecutionResult.Rejected<SolutionStructureData>(
                        "ProjectStructureUnavailable",
                        targetFrameworks.ErrorMessage,
                        RequiredAction.Retry);
                }

                var projectPath = projectEntry.Path;
                var solutionFolderPath = hierarchy.ProjectFolderPaths.TryGetValue(projectPath, out var folderPath)
                    ? folderPath
                    : null;

                ProjectReferenceInfo[] projectReferences;
                using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.ProjectReferenceProjectionPhase))
                {
                    var projectedProjectReferences = new List<ProjectReferenceInfo>();
                    foreach (var reference in project.ProjectReferences)
                    {
                        var referencedProject = context.CurrentSolution.GetProject(reference.ProjectId);
                        if (referencedProject is not null
                            && context.WorkspacePathService.TryNormalizePath(referencedProject.FilePath ?? referencedProject.Name, out var referencedProjectPath))
                        {
                            projectedProjectReferences.Add(InspectionProjectionFactory.CreateProjectReferenceInfo(referencedProject, referencedProjectPath));
                        }
                    }

                    projectReferences = projectedProjectReferences
                        .OrderBy(static reference => reference.Path, StringComparer.Ordinal)
                        .ToArray();
                }

                IReadOnlyList<DocumentReference>? documents = null;
                if (request.IncludeDocuments)
                {
                    using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.DocumentProjectionPhase))
                    {
                        var projectedDocuments = new List<DocumentReference>();
                        foreach (var document in project.Documents)
                        {
                            var documentReference = context.WorkspaceResolver.CreateDocumentReference(document);
                            if (documentReference is not null)
                            {
                                projectedDocuments.Add(documentReference);
                            }
                        }

                        documents = projectedDocuments
                            .OrderBy(static document => document.Path, StringComparer.Ordinal)
                            .ToArray();
                    }
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
        }

        var data = new SolutionStructureData
        {
            SolutionPath = context.WorkspaceIdentity.LoadedPath,
            Folders = BoundedCollection.CreatePrebounded(folders, hierarchy.Folders.Count),
            Projects = BoundedCollection.CreatePrebounded(projectStructures, context.CurrentSolution.ProjectIds.Count),
        };

        return PluginExecutionResult.Success(data);
    }
}
