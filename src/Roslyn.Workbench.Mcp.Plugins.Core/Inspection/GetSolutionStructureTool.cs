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
            hierarchy = await context.ToolExecutionServices.ProjectStructureService.GetSolutionHierarchyAsync(context.WorkspaceIdentity.LoadedPath, cancellationToken);
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

        var selectedProjects = new List<Project>();
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.ProjectSelectionPhase))
        {
            var orderedProjects = context.CurrentSolution.Projects
                .OrderBy(project => context.WorkspaceResolver.NormalizeProjectPath(project.FilePath ?? project.Name), StringComparer.Ordinal);

            foreach (var project in orderedProjects)
            {
                if (selectedProjects.Count == request.EffectiveProjectsLimit)
                {
                    break;
                }

                selectedProjects.Add(project);
            }
        }

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
            for (var index = 0; index < selectedProjects.Count; index++)
            {
                var project = selectedProjects[index];
                var targetFrameworks = targetFrameworkResults[index];

                if (!targetFrameworks.IsSucceeded)
                {
                    return PluginExecutionResult.Rejected<SolutionStructureData>(
                        "ProjectStructureUnavailable",
                        targetFrameworks.ErrorMessage,
                        RequiredAction.Retry);
                }

                var projectPath = context.WorkspaceResolver.NormalizeProjectPath(project.FilePath ?? project.Name);
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
                        if (referencedProject is not null)
                        {
                            projectedProjectReferences.Add(InspectionProjectionFactory.CreateProjectReferenceInfo(referencedProject, context.WorkspaceResolver));
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
